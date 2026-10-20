using System.Net;
using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopSoulseekSharingViewModelTests
{
    [TestMethod]
    public async Task RefreshAsync_MapsSnapshotCountsAndRootStatus()
    {
        var snapshot = Snapshot([Root(enabled: true, included: true, statusCode: "ready")], revision: 7, directories: 3, files: 2);
        var uploads = UploadSnapshot(
            [Upload(filename: "track.flac", isTerminal: false)],
            revision: 9,
            active: 1,
            terminal: 2,
            truncated: 3);
        var handler = new QueueHandler(_ => JsonResponse(snapshot), _ => JsonResponse(uploads));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSoulseekSharingViewModel(new SockseekApiClient(http));

        Assert.IsTrue(await viewModel.RefreshAsync());

        CollectionAssert.AreEqual(
            new[] { "api/v1/soulseek/sharing", "api/v1/soulseek/uploads" },
            handler.Requests.Select(request => request.Path).ToArray());
        Assert.AreEqual("2 files across 3 folders | revision 7", viewModel.SnapshotSummary);
        Assert.AreEqual("ready | 2 files / 3 folders", viewModel.Roots.Single().StatusSummary);
        Assert.AreEqual("1 active | 2 recent | 3 omitted | revision 9", viewModel.UploadSummary);
        Assert.AreEqual("track.flac", viewModel.Uploads.Single().Filename);
        Assert.AreEqual("25.0 KB / 100.0 KB | 25% | 5.0 KB/s", viewModel.Uploads.Single().ProgressSummary);
        Assert.IsTrue(viewModel.HasRoots);
        Assert.IsFalse(viewModel.IsEmpty);
        Assert.IsTrue(viewModel.HasUploads);
    }

    [TestMethod]
    public async Task SaveAsync_ValidatesLocallyThenPostsTrimmedRootAndAppliesSnapshot()
    {
        var root = Root(enabled: true, included: true, statusCode: "ready");
        var handler = new QueueHandler(_ => JsonResponse(Snapshot([root], revision: 2, directories: 1, files: 1)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSoulseekSharingViewModel(new SockseekApiClient(http));

        Assert.IsFalse(await viewModel.SaveAsync());
        StringAssert.Contains(viewModel.ErrorMessage, "root path");
        Assert.AreEqual(0, handler.Requests.Count);

        viewModel.RootPath = " C:/Music ";
        viewModel.VirtualName = " Music ";
        Assert.IsTrue(await viewModel.SaveAsync());

        Assert.AreEqual(HttpMethod.Post, handler.Requests.Single().Method);
        Assert.AreEqual("api/v1/soulseek/sharing/roots", handler.Requests.Single().Path);
        StringAssert.Contains(handler.Requests.Single().Body, "C:/Music");
        StringAssert.Contains(handler.Requests.Single().Body, "Music");
        Assert.AreEqual("Share root added.", viewModel.OperationSummary);
        Assert.AreEqual(string.Empty, viewModel.RootPath);
        Assert.AreEqual(1, viewModel.FileCount);
    }

    [TestMethod]
    public async Task EditToggleDeleteAndRebuild_UseExplicitRootActions()
    {
        var root = Root(enabled: true, included: true, statusCode: "ready");
        var updated = root with { VirtualName = "Archive" };
        var disabled = updated with { Enabled = false, Included = false, StatusCode = "disabled", DirectoryCount = 0, FileCount = 0 };
        var handler = new QueueHandler(
            _ => JsonResponse(Snapshot([updated], revision: 2, directories: 3, files: 2)),
            _ => JsonResponse(Snapshot([disabled], revision: 3)),
            _ => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ => JsonResponse(Snapshot([], revision: 4)),
            _ => JsonResponse(Snapshot([], revision: 5)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSoulseekSharingViewModel(new SockseekApiClient(http));
        var row = new DesktopSoulseekShareRootViewModel(root);

        viewModel.SelectedRoot = row;
        viewModel.VirtualName = "Archive";
        Assert.IsTrue(await viewModel.SaveAsync());
        Assert.AreEqual(HttpMethod.Put, handler.Requests[0].Method);
        Assert.AreEqual($"api/v1/soulseek/sharing/roots/{root.Id}", handler.Requests[0].Path);
        Assert.AreEqual("Share root saved.", viewModel.OperationSummary);

        Assert.IsTrue(await viewModel.ToggleEnabledAsync(new DesktopSoulseekShareRootViewModel(updated)));
        Assert.AreEqual(HttpMethod.Put, handler.Requests[1].Method);
        Assert.AreEqual("Share root disabled.", viewModel.OperationSummary);

        Assert.IsTrue(await viewModel.DeleteAsync(new DesktopSoulseekShareRootViewModel(disabled)));
        Assert.AreEqual(HttpMethod.Delete, handler.Requests[2].Method);
        Assert.AreEqual(HttpMethod.Get, handler.Requests[3].Method);
        StringAssert.Contains(viewModel.OperationSummary, "Local audio was not deleted");

        Assert.IsTrue(await viewModel.RebuildAsync());
        Assert.AreEqual("api/v1/soulseek/sharing/rebuild", handler.Requests[4].Path);
        Assert.AreEqual("Share snapshot rebuilt.", viewModel.OperationSummary);
    }

    [TestMethod]
    public async Task RefreshAsync_DaemonUnavailableShowsRecoveryMessage()
    {
        var handler = new QueueHandler(
            _ => throw new HttpRequestException("connection refused"),
            _ => throw new HttpRequestException("connection refused"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSoulseekSharingViewModel(new SockseekApiClient(http));

        Assert.IsFalse(await viewModel.RefreshAsync());

        StringAssert.Contains(viewModel.ErrorMessage, "Wait for it to reconnect");
        StringAssert.Contains(viewModel.UploadErrorMessage, "Wait for it to reconnect");
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsFalse(viewModel.IsUploadBusy);
    }

    [TestMethod]
    public async Task RefreshUploadsAsync_WhenInvalidatedDuringActiveRequest_CoalescesOneFollowUpSnapshot()
    {
        var handler = new CoalescingUploadHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSoulseekSharingViewModel(new SockseekApiClient(http));

        Task<bool> firstRefresh = viewModel.RefreshUploadsAsync();
        await handler.FirstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsFalse(await viewModel.RefreshUploadsAsync());
        handler.ReleaseFirstRequest.TrySetResult();
        Assert.IsTrue(await firstRefresh);
        await handler.SecondRequestReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(2, handler.RequestCount);
        await WaitForConditionAsync(() => viewModel.UploadRevision == 2, TimeSpan.FromSeconds(5));
        Assert.AreEqual("second.flac", viewModel.Uploads.Single().Filename);
    }

    [TestMethod]
    public void SharingSurface_IsBoundedAndVirtualized()
    {
        string xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Sockseek.Desktop",
            "DesktopShellMainWindow.axaml"));
        string xaml = File.ReadAllText(xamlPath);
        const string binding = "<ListBox ItemsSource=\"{Binding Sharing.Roots}\"";
        int start = xaml.IndexOf(binding, StringComparison.Ordinal);
        int end = xaml.IndexOf("</ListBox>", start, StringComparison.Ordinal);

        Assert.IsTrue(start >= 0);
        Assert.IsTrue(end > start);
        string surface = xaml[start..end];
        StringAssert.Contains(surface, "MaxHeight=\"360\"");
        StringAssert.Contains(surface, "<VirtualizingStackPanel />");
        StringAssert.Contains(surface, "Content=\"Remove\"");

        const string uploadBinding = "<ListBox ItemsSource=\"{Binding Sharing.Uploads}\"";
        int uploadStart = xaml.IndexOf(uploadBinding, StringComparison.Ordinal);
        int uploadEnd = xaml.IndexOf("</ListBox>", uploadStart, StringComparison.Ordinal);
        Assert.IsTrue(uploadStart >= 0);
        Assert.IsTrue(uploadEnd > uploadStart);
        string uploadSurface = xaml[uploadStart..uploadEnd];
        StringAssert.Contains(uploadSurface, "MaxHeight=\"320\"");
        StringAssert.Contains(uploadSurface, "<VirtualizingStackPanel />");
        StringAssert.Contains(uploadSurface, "Text=\"{Binding ProgressSummary}\"");
    }

    private static SoulseekSharingSnapshotDto Snapshot(
        IReadOnlyList<SoulseekShareRootDto> roots,
        long revision,
        int directories = 0,
        int files = 0)
        => new(revision, directories, files, roots);

    private static SoulseekShareRootDto Root(bool enabled, bool included, string statusCode)
    {
        var now = new DateTimeOffset(2026, 10, 20, 16, 0, 0, TimeSpan.Zero);
        return new SoulseekShareRootDto(
            Guid.NewGuid(),
            "C:/Music",
            "Music",
            enabled,
            included,
            statusCode,
            included ? 3 : 0,
            included ? 2 : 0,
            now,
            now);
    }

    private static SoulseekUploadSnapshotDto UploadSnapshot(
        IReadOnlyList<SoulseekUploadDto> uploads,
        long revision,
        int active = 0,
        int terminal = 0,
        int truncated = 0)
        => new(revision, active, terminal, truncated, uploads);

    private static SoulseekUploadDto Upload(string filename, bool isTerminal)
        => new(
            Guid.NewGuid(),
            "listener",
            filename,
            isTerminal ? "Completed, Succeeded" : "InProgress",
            100 * 1024,
            isTerminal ? 100 * 1024 : 25 * 1024,
            5 * 1024,
            new DateTimeOffset(2026, 10, 20, 16, 0, 0, TimeSpan.Zero),
            isTerminal ? new DateTimeOffset(2026, 10, 20, 16, 1, 0, TimeSpan.Zero) : null,
            isTerminal);

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
            string body = request.Content is null
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

    private sealed class CoalescingUploadHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public TaskCompletionSource FirstRequestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondRequestReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.AreEqual("api/v1/soulseek/uploads", request.RequestUri?.PathAndQuery.TrimStart('/'));
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

            return JsonResponse(UploadSnapshot(
                [Upload(requestNumber == 1 ? "first.flac" : "second.flac", isTerminal: false)],
                revision: requestNumber,
                active: 1));
        }
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.IsTrue(predicate(), "Expected upload view state was not reached before the timeout.");
    }

    private sealed record CapturedRequest(HttpMethod Method, string Path, string Body);
}
