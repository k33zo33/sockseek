using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopShellWindowViewModelTests
{
    [TestMethod]
    public async Task Constructor_ExposesDefaultShellChromeState()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        var viewModel = new DesktopShellWindowViewModel(session);

        Assert.AreEqual("Shell.Window.Title", viewModel.TitleResourceKey);
        Assert.AreEqual("Sockseek", viewModel.Title);
        Assert.AreEqual("Sockseek — Home", viewModel.WindowTitle);
        Assert.AreEqual(DesktopDesignTokens.Surface.AppCanvas, viewModel.SurfaceToken);
        Assert.AreEqual(DesktopDesignTokens.Spacing.ShellChrome, viewModel.ChromeSpacingToken);
        Assert.AreSame(session.Shell, viewModel.Shell);
        Assert.AreSame(session.Shell.PlayerBar, viewModel.PlayerBar);
        Assert.AreSame(session.Shell.StatusBanner, viewModel.StatusBanner);
        Assert.AreEqual(ShellSection.Home, viewModel.CurrentPage.Section);
        Assert.IsFalse(viewModel.CanCopyDiagnostics);
        Assert.IsNull(viewModel.CopyDiagnosticsLabel);
        Assert.IsFalse(viewModel.CanStartDaemon);
        Assert.AreEqual("Start local daemon", viewModel.StartDaemonLabel);
        Assert.AreEqual("Shell.Backend.Action.StartDaemon.Label", viewModel.StartDaemonLabelResourceKey);
        Assert.AreEqual("Try starting the local daemon again", viewModel.StartDaemonHint);
        Assert.AreEqual("Shell.Backend.Action.StartDaemon.Hint", viewModel.StartDaemonHintResourceKey);
        StringAssert.Contains(viewModel.DiagnosticsText, "Page: Home");
    }

    [TestMethod]
    public async Task WindowViewModel_ReflectsNavigationThemeAndDiagnosticsUpdatesFromShellSession()
    {
        var supervisor = new DesktopDaemonSupervisor();
        var store = new InMemoryDesktopThemePreferenceStore(DesktopThemePreference.System);
        await using var session = new DesktopShellSession(
            supervisor: supervisor,
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake),
            themePreferenceStore: store);
        var viewModel = new DesktopShellWindowViewModel(session);

        session.Shell.NavigateTo(ShellSection.Downloads);
        session.Shell.SetTheme(DesktopThemePreference.Dark);
        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"secret-token\"}");
        session.Shell.SetBackendState(BackendConnectionState.Disconnected);

        Assert.AreEqual("Sockseek — Downloads", viewModel.WindowTitle);
        Assert.AreEqual(ShellSection.Downloads, viewModel.CurrentPage.Section);
        Assert.AreEqual(DesktopThemePreference.Dark, viewModel.CurrentTheme);
        Assert.IsTrue(viewModel.CanCopyDiagnostics);
        Assert.IsFalse(viewModel.CanStartDaemon);
        Assert.AreEqual("Copy diagnostics", viewModel.CopyDiagnosticsLabel);
        Assert.AreEqual("Shell.Backend.Action.CopyDiagnostics.Label", viewModel.CopyDiagnosticsLabelResourceKey);
        Assert.AreEqual("Copy backend diagnostics", viewModel.CopyDiagnosticsHint);
        Assert.AreEqual("Shell.Backend.Action.CopyDiagnostics.Hint", viewModel.CopyDiagnosticsHintResourceKey);
        StringAssert.Contains(viewModel.DiagnosticsText, "Page: Downloads");
        StringAssert.Contains(viewModel.DiagnosticsText, "Theme: Dark");
        StringAssert.Contains(viewModel.DiagnosticsText, "Backend state: Disconnected");
        StringAssert.Contains(viewModel.DiagnosticsText, "Backend URL: unavailable");
        Assert.IsFalse(viewModel.DiagnosticsText.Contains("secret-token", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task WindowViewModel_WhenSessionCanLaunchAndBackendDisconnects_ExposesStartDaemonAction()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(new FakeProcessLauncher()),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake),
            workspaceRoot: "/workspace",
            launchRequestFactory: root => new DesktopDaemonLaunchRequest(
                "dotnet",
                "run --project Sockseek.Server/Sockseek.Server.csproj",
                root,
                new Dictionary<string, string?>()));
        var viewModel = new DesktopShellWindowViewModel(session);

        session.Shell.SetBackendState(BackendConnectionState.Disconnected);

        Assert.IsTrue(session.CanStartDaemon);
        Assert.IsTrue(viewModel.CanStartDaemon);
        Assert.AreEqual("Start local daemon", viewModel.StartDaemonLabel);
        Assert.AreEqual("Try starting the local daemon again", viewModel.StartDaemonHint);
    }

    private sealed class FakeProcessLauncher : IDesktopProcessLauncher
    {
        public Task<IDesktopProcessSession> LaunchAsync(DesktopDaemonLaunchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IDesktopProcessSession>(new FakeProcessSession());
    }

    private sealed class FakeProcessSession : IDesktopProcessSession
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async IAsyncEnumerable<string> ReadOutputLinesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield break;
        }
    }

    private sealed class FakeDesktopEventHubConnection(DesktopDaemonHandshake handshake) : IDesktopEventHubConnection
    {
        public DesktopDaemonHandshake Handshake { get; } = handshake;

        public event Func<Exception?, Task>? Reconnecting
        {
            add { }
            remove { }
        }

        public event Func<string?, Task>? Reconnected
        {
            add { }
            remove { }
        }

        public event Func<Exception?, Task>? Closed
        {
            add { }
            remove { }
        }

        public void OnServerEvent(Func<Sockseek.Api.ServerEventEnvelopeDto, Task> handler)
            => _ = handler;

        public void OnWorkflowUpdateBatch(Func<Sockseek.Api.WorkflowUpdateBatchDto, Task> handler)
            => _ = handler;

        public Task StartAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SubscribeAllAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SubscribeWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
