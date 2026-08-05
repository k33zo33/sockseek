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
    }

    [TestMethod]
    public async Task WindowViewModel_ReflectsNavigationAndThemeUpdatesFromShellSession()
    {
        var store = new InMemoryDesktopThemePreferenceStore(DesktopThemePreference.System);
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake),
            themePreferenceStore: store);
        var viewModel = new DesktopShellWindowViewModel(session);

        session.Shell.NavigateTo(ShellSection.Downloads);
        session.Shell.SetTheme(DesktopThemePreference.Dark);

        Assert.AreEqual("Sockseek — Downloads", viewModel.WindowTitle);
        Assert.AreEqual(ShellSection.Downloads, viewModel.CurrentPage.Section);
        Assert.AreEqual(DesktopThemePreference.Dark, viewModel.CurrentTheme);
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
