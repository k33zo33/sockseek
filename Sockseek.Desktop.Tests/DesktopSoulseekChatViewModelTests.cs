using System.Net;
using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopSoulseekChatViewModelTests
{
    [TestMethod]
    public async Task RefreshAsync_MapsBoundedSnapshotAndPreservesRoomSelection()
    {
        var occurredAt = new DateTimeOffset(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);
        var first = Snapshot(
            revision: 7,
            publicChatEnabled: true,
            availableRooms:
            [
                new SoulseekChatRoomDto("music", 12, false),
                new SoulseekChatRoomDto("invite-only", 2, true),
            ],
            joinedRooms:
            [
                new SoulseekJoinedChatRoomDto("music", false, ["listener", "sender"]),
                new SoulseekJoinedChatRoomDto("jazz", false, ["listener"]),
            ],
            messages:
            [
                new SoulseekChatMessageDto(Guid.NewGuid(), "private", null, "sender", "hello", occurredAt, false, true),
                new SoulseekChatMessageDto(Guid.NewGuid(), "room", "music", "listener", "outgoing", occurredAt, true, false),
            ]);
        var second = first with
        {
            Revision = 8,
            JoinedRooms = [first.JoinedRooms[1], first.JoinedRooms[0]],
        };
        var handler = new QueueHandler(_ => JsonResponse(first), _ => JsonResponse(second));
        using var http = Client(handler);
        var viewModel = new DesktopSoulseekChatViewModel(new SockseekApiClient(http));

        Assert.IsTrue(await viewModel.RefreshAsync());
        Assert.IsTrue(viewModel.IsLoaded);
        Assert.AreEqual(7, viewModel.Revision);
        Assert.AreEqual("Revision 7", viewModel.RevisionSummary);
        Assert.IsTrue(viewModel.PublicChatEnabled);
        Assert.AreEqual("Stop public chat", viewModel.PublicChatActionLabel);
        Assert.AreEqual("12 users", viewModel.AvailableRooms[0].UserCountSummary);
        Assert.AreEqual("Private room", viewModel.AvailableRooms[1].AccessSummary);
        Assert.AreEqual("music", viewModel.SelectedJoinedRoom?.Name);
        Assert.AreEqual("Private", viewModel.Messages[0].ScopeSummary);
        Assert.AreEqual("Replayed", viewModel.Messages[0].DeliverySummary);
        Assert.AreEqual("Room #music", viewModel.Messages[1].ScopeSummary);
        Assert.AreEqual("You", viewModel.Messages[1].SenderSummary);

        viewModel.SelectedJoinedRoom = viewModel.JoinedRooms.Single(room => room.Name == "jazz");
        Assert.IsTrue(await viewModel.RefreshAsync());

        Assert.AreEqual(8, viewModel.Revision);
        Assert.AreEqual("jazz", viewModel.SelectedJoinedRoom?.Name);
        Assert.IsTrue(viewModel.HasAvailableRooms);
        Assert.IsTrue(viewModel.HasJoinedRooms);
        Assert.IsTrue(viewModel.HasMessages);
        Assert.IsNull(viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task Commands_PostExplicitActionsApplySnapshotsAndClearSentText()
    {
        var joined = Snapshot(
            1,
            joinedRooms: [new SoulseekJoinedChatRoomDto("music", false, ["listener"])]);
        var roomSent = joined with
        {
            Revision = 2,
            Messages = [Message("room", "music", "listener", "room text", outgoing: true)],
        };
        var privateSent = roomSent with
        {
            Revision = 3,
            Messages =
            [
                roomSent.Messages[0],
                Message("private", null, "friend", "private text", outgoing: true),
            ],
        };
        var publicStarted = privateSent with { Revision = 4, PublicChatEnabled = true };
        var left = publicStarted with { Revision = 5, JoinedRooms = [] };
        var handler = new QueueHandler(
            _ => JsonResponse(joined),
            _ => JsonResponse(roomSent),
            _ => JsonResponse(privateSent),
            _ => JsonResponse(publicStarted),
            _ => JsonResponse(left));
        using var http = Client(handler);
        var viewModel = new DesktopSoulseekChatViewModel(new SockseekApiClient(http))
        {
            RoomName = "  music  ",
            RoomMessage = "room text",
            PrivateUsername = "friend",
            PrivateMessage = "private text",
        };

        Assert.IsTrue(await viewModel.JoinRoomAsync());
        Assert.AreEqual(string.Empty, viewModel.RoomName);
        Assert.AreEqual("music", viewModel.SelectedJoinedRoom?.Name);

        Assert.IsTrue(await viewModel.SendRoomMessageAsync());
        Assert.AreEqual(string.Empty, viewModel.RoomMessage);

        Assert.IsTrue(await viewModel.SendPrivateMessageAsync());
        Assert.AreEqual(string.Empty, viewModel.PrivateMessage);
        Assert.AreEqual("friend", viewModel.PrivateUsername);

        Assert.IsTrue(await viewModel.SetPublicChatEnabledAsync(true));
        Assert.IsTrue(viewModel.PublicChatEnabled);

        var joinedRoom = viewModel.JoinedRooms.Single();
        Assert.IsTrue(await viewModel.LeaveRoomAsync(joinedRoom));
        Assert.IsFalse(viewModel.HasJoinedRooms);
        Assert.IsTrue(viewModel.IsJoinedRoomsEmpty);

        CollectionAssert.AreEqual(
            new[]
            {
                "api/v1/soulseek/chat/rooms/join",
                "api/v1/soulseek/chat/rooms/messages",
                "api/v1/soulseek/chat/private/messages",
                "api/v1/soulseek/chat/public/start",
                "api/v1/soulseek/chat/rooms/leave",
            },
            handler.Requests.Select(request => request.Path).ToArray());
        Assert.IsTrue(handler.Requests.All(request => request.Method == HttpMethod.Post));
        StringAssert.Contains(handler.Requests[0].Body, "music");
        StringAssert.Contains(handler.Requests[1].Body, "room text");
        StringAssert.Contains(handler.Requests[2].Body, "private text");
    }

    [TestMethod]
    public async Task InputValidation_RejectsEmptyAndOversizedContentWithoutCallingDaemon()
    {
        var handler = new QueueHandler();
        using var http = Client(handler);
        var viewModel = new DesktopSoulseekChatViewModel(new SockseekApiClient(http));

        Assert.IsFalse(await viewModel.JoinRoomAsync());
        StringAssert.Contains(viewModel.ErrorMessage, "room name");

        viewModel.RoomName = new string('r', 129);
        Assert.IsFalse(await viewModel.JoinRoomAsync());
        StringAssert.Contains(viewModel.ErrorMessage, "128");

        viewModel.PrivateUsername = "friend";
        viewModel.PrivateMessage = new string('m', 4097);
        Assert.IsFalse(await viewModel.SendPrivateMessageAsync());
        StringAssert.Contains(viewModel.ErrorMessage, "4096");

        viewModel.RoomMessage = "hello";
        Assert.IsFalse(await viewModel.SendRoomMessageAsync());
        StringAssert.Contains(viewModel.ErrorMessage, "Select a joined room");
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task RefreshAsync_DaemonUnavailableShowsRecoveryMessage()
    {
        var handler = new QueueHandler(_ => throw new HttpRequestException("connection refused"));
        using var http = Client(handler);
        var viewModel = new DesktopSoulseekChatViewModel(new SockseekApiClient(http));

        Assert.IsFalse(await viewModel.RefreshAsync());

        StringAssert.Contains(viewModel.ErrorMessage, "Wait for it to reconnect");
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsFalse(viewModel.IsLoaded);
    }

    [TestMethod]
    public async Task RefreshAsync_WhenInvalidatedDuringActiveRequest_CoalescesOneFollowUpSnapshot()
    {
        var handler = new CoalescingHandler();
        using var http = Client(handler);
        var viewModel = new DesktopSoulseekChatViewModel(new SockseekApiClient(http));

        Task<bool> firstRefresh = viewModel.RefreshAsync();
        await handler.FirstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsFalse(await viewModel.RefreshAsync());
        handler.ReleaseFirstRequest.TrySetResult();
        Assert.IsTrue(await firstRefresh);
        await handler.SecondRequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => viewModel.Revision == 2, TimeSpan.FromSeconds(5));

        Assert.AreEqual(2, handler.RequestCount);
        Assert.AreEqual(2, viewModel.Revision);
    }

    [TestMethod]
    public void Commands_RequireTypedRoomsOnlyForRowActions()
    {
        var handler = new QueueHandler();
        using var http = Client(handler);
        var viewModel = new DesktopSoulseekChatViewModel(new SockseekApiClient(http));
        var available = new DesktopSoulseekChatRoomViewModel(new SoulseekChatRoomDto("music", 1, false));
        var joined = new DesktopSoulseekJoinedChatRoomViewModel(
            new SoulseekJoinedChatRoomDto("music", false, ["listener"]));

        Assert.IsTrue(viewModel.RefreshCommand.CanExecute(null));
        Assert.IsTrue(viewModel.RefreshRoomsCommand.CanExecute(null));
        Assert.IsTrue(viewModel.JoinRoomCommand.CanExecute(null));
        Assert.IsTrue(viewModel.SendPrivateMessageCommand.CanExecute(null));
        Assert.IsTrue(viewModel.SendRoomMessageCommand.CanExecute(null));
        Assert.IsTrue(viewModel.TogglePublicChatCommand.CanExecute(null));
        Assert.IsTrue(viewModel.JoinSelectedRoomCommand.CanExecute(available));
        Assert.IsTrue(viewModel.LeaveRoomCommand.CanExecute(joined));
        Assert.IsFalse(viewModel.JoinSelectedRoomCommand.CanExecute(null));
        Assert.IsFalse(viewModel.LeaveRoomCommand.CanExecute(new object()));
    }

    [TestMethod]
    public void ChatSurface_UsesExplicitActionsAndBoundedVirtualizedLists()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Sockseek.Desktop",
            "DesktopShellMainWindow.axaml"));
        string xaml = File.ReadAllText(xamlPath);

        AssertVirtualizedList(xaml, "<ListBox ItemsSource=\"{Binding Chat.AvailableRooms}\"", "MaxHeight=\"260\"");
        AssertVirtualizedList(xaml, "<ListBox ItemsSource=\"{Binding Chat.JoinedRooms}\"", "MaxHeight=\"260\"");
        AssertVirtualizedList(xaml, "<ListBox ItemsSource=\"{Binding Chat.Messages}\"", "MaxHeight=\"420\"");
        StringAssert.Contains(xaml, "Command=\"{Binding Chat.TogglePublicChatCommand}\"");
        StringAssert.Contains(xaml, "Command=\"{Binding Chat.SendPrivateMessageCommand}\"");
        StringAssert.Contains(xaml, "Command=\"{Binding Chat.SendRoomMessageCommand}\"");
    }

    private static void AssertVirtualizedList(string xaml, string binding, string maximumHeight)
    {
        int start = xaml.IndexOf(binding, StringComparison.Ordinal);
        int end = xaml.IndexOf("</ListBox>", start, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"Expected chat list binding '{binding}'.");
        Assert.IsTrue(end > start, "Expected a complete chat ListBox template.");
        string listXaml = xaml[start..end];
        StringAssert.Contains(listXaml, maximumHeight);
        StringAssert.Contains(listXaml, "<VirtualizingStackPanel />");
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate() && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.IsTrue(predicate(), "The expected asynchronous chat state was not reached in time.");
    }

    private static SoulseekChatSnapshotDto Snapshot(
        long revision,
        bool publicChatEnabled = false,
        IReadOnlyList<SoulseekChatRoomDto>? availableRooms = null,
        IReadOnlyList<SoulseekJoinedChatRoomDto>? joinedRooms = null,
        IReadOnlyList<SoulseekChatMessageDto>? messages = null)
        => new(
            revision,
            publicChatEnabled,
            availableRooms ?? [],
            joinedRooms ?? [],
            messages ?? []);

    private static SoulseekChatMessageDto Message(
        string kind,
        string? roomName,
        string username,
        string message,
        bool outgoing = false)
        => new(
            Guid.NewGuid(),
            kind,
            roomName,
            username,
            message,
            new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero),
            outgoing,
            false);

    private static HttpClient Client(HttpMessageHandler handler)
        => new(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };

    private static HttpResponseMessage JsonResponse<T>(T value, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode) { Content = JsonContent.Create(value) };

    private sealed class QueueHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = new(responses);

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty,
                body));

            if (responses.Count == 0)
                throw new InvalidOperationException("No queued response is available.");

            return responses.Dequeue()(request);
        }
    }

    private sealed class CoalescingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public TaskCompletionSource FirstRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondRequestReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.AreEqual("api/v1/soulseek/chat", request.RequestUri?.PathAndQuery.TrimStart('/'));
            RequestCount++;
            int requestNumber = RequestCount;
            if (requestNumber == 1)
            {
                FirstRequestStarted.TrySetResult();
                await ReleaseFirstRequest.Task.WaitAsync(cancellationToken);
            }
            else
            {
                SecondRequestReceived.TrySetResult();
            }

            return JsonResponse(Snapshot(requestNumber));
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Path, string Body);
}
