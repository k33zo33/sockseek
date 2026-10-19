using System.Net;
using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopSoulseekPrivilegesViewModelTests
{
    [TestMethod]
    public async Task RefreshAsync_MapsPrivilegeStatusAndPendingNotifications()
    {
        var receivedAt = new DateTimeOffset(2026, 10, 19, 8, 30, 0, TimeSpan.Zero);
        var status = new SoulseekPrivilegeStatusDto(
            12,
            new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero),
            [new SoulseekPrivilegeNotificationDto(Guid.NewGuid(), "listener", receivedAt, true)]);
        var handler = new QueueHandler(_ => JsonResponse(status));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSoulseekPrivilegesViewModel(new SockseekApiClient(http));

        Assert.IsTrue(await viewModel.RefreshAsync());

        Assert.IsTrue(viewModel.IsLoaded);
        Assert.AreEqual(12, viewModel.RemainingDays);
        Assert.AreEqual("12 privilege days remaining", viewModel.StatusSummary);
        Assert.IsTrue(viewModel.HasNotifications);
        Assert.IsFalse(viewModel.IsEmpty);
        var notification = viewModel.Notifications.Single();
        Assert.AreEqual("listener", notification.Username);
        Assert.IsTrue(notification.RequiresAcknowledgement);
        Assert.AreEqual("Acknowledgement required", notification.AcknowledgementSummary);
        Assert.IsNull(viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task AcknowledgeAsync_PostsExplicitActionThenReloadsSnapshot()
    {
        var id = Guid.NewGuid();
        var notification = new DesktopSoulseekPrivilegeNotificationViewModel(
            new SoulseekPrivilegeNotificationDto(id, "listener", DateTimeOffset.UtcNow, true));
        var refreshed = new SoulseekPrivilegeStatusDto(3, DateTimeOffset.UtcNow, []);
        var handler = new QueueHandler(
            _ => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ => JsonResponse(refreshed));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSoulseekPrivilegesViewModel(new SockseekApiClient(http));

        Assert.IsTrue(await viewModel.AcknowledgeAsync(notification));

        Assert.AreEqual(2, handler.Requests.Count);
        Assert.AreEqual(HttpMethod.Post, handler.Requests[0].Method);
        Assert.AreEqual($"api/v1/soulseek/privileges/notifications/{id}/acknowledge", handler.Requests[0].Path);
        Assert.AreEqual(HttpMethod.Get, handler.Requests[1].Method);
        Assert.AreEqual("api/v1/soulseek/privileges", handler.Requests[1].Path);
        Assert.AreEqual("Privilege notification acknowledged.", viewModel.OperationSummary);
        Assert.IsTrue(viewModel.IsEmpty);
    }

    [TestMethod]
    public async Task AcknowledgeAsync_WhenNotificationDoesNotRequireIt_DoesNotCallDaemon()
    {
        var handler = new QueueHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSoulseekPrivilegesViewModel(new SockseekApiClient(http));
        var notification = new DesktopSoulseekPrivilegeNotificationViewModel(
            new SoulseekPrivilegeNotificationDto(Guid.NewGuid(), "listener", DateTimeOffset.UtcNow, false));

        Assert.IsFalse(await viewModel.AcknowledgeAsync(notification));

        StringAssert.Contains(viewModel.ErrorMessage, "does not require acknowledgement");
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task RefreshAsync_DaemonUnavailableShowsRecoveryMessage()
    {
        var handler = new QueueHandler(_ => throw new HttpRequestException("connection refused"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSoulseekPrivilegesViewModel(new SockseekApiClient(http));

        Assert.IsFalse(await viewModel.RefreshAsync());

        StringAssert.Contains(viewModel.ErrorMessage, "Wait for it to reconnect");
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsFalse(viewModel.IsLoaded);
    }

    [TestMethod]
    public void Commands_RequireTypedNotificationForAcknowledgement()
    {
        var handler = new QueueHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSoulseekPrivilegesViewModel(new SockseekApiClient(http));
        var notification = new DesktopSoulseekPrivilegeNotificationViewModel(
            new SoulseekPrivilegeNotificationDto(Guid.NewGuid(), "listener", DateTimeOffset.UtcNow, true));

        Assert.IsTrue(viewModel.RefreshCommand.CanExecute(null));
        Assert.IsTrue(viewModel.AcknowledgeCommand.CanExecute(notification));
        Assert.IsFalse(viewModel.AcknowledgeCommand.CanExecute(null));
        Assert.IsFalse(viewModel.AcknowledgeCommand.CanExecute(new object()));
    }

    [TestMethod]
    public void PrivilegeSurface_IsBoundedVirtualizedAndAcknowledgementIsExplicit()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Sockseek.Desktop",
            "DesktopShellMainWindow.axaml"));
        var xaml = File.ReadAllText(xamlPath);
        const string privilegeBinding = "<ListBox ItemsSource=\"{Binding Search.Privileges.Notifications}\"";
        int privilegeStart = xaml.IndexOf(privilegeBinding, StringComparison.Ordinal);
        int privilegeEnd = xaml.IndexOf("</ListBox>", privilegeStart, StringComparison.Ordinal);

        Assert.IsTrue(privilegeStart >= 0, "The Search workspace should expose pending privilege notifications.");
        Assert.IsTrue(privilegeEnd > privilegeStart, "The privilege ListBox should have a complete template.");
        string privilegeXaml = xaml[privilegeStart..privilegeEnd];
        StringAssert.Contains(privilegeXaml, "MaxHeight=\"220\"");
        StringAssert.Contains(privilegeXaml, "<VirtualizingStackPanel />");
        StringAssert.Contains(privilegeXaml, "Content=\"Acknowledge\"");
        StringAssert.Contains(privilegeXaml, "IsVisible=\"{Binding RequiresAcknowledgement}\"");
    }

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

    private sealed record CapturedRequest(HttpMethod Method, string Path, string Body);
}
