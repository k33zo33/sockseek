using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopShellKeyRoutingTests
{
    [TestMethod]
    public async Task TryHandleShellInput_ClosePaletteRequest_ClosesOpenCommandPalette()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        using var viewModel = new DesktopShellWindowViewModel(session);
        viewModel.OpenCommandPalette();

        var handled = DesktopShellKeyRouting.TryHandleShellInput(viewModel, shortcut: null, shouldClosePalette: true);

        Assert.IsTrue(handled);
        Assert.IsFalse(viewModel.IsCommandPaletteOpen);
    }

    [DataTestMethod]
    [DataRow("Ctrl+1", ShellSection.Home)]
    [DataRow("Ctrl+L", ShellSection.Search)]
    [DataRow("Ctrl+2", ShellSection.Playlists)]
    [DataRow("Ctrl+3", ShellSection.Library)]
    [DataRow("Ctrl+4", ShellSection.Downloads)]
    [DataRow("Ctrl+5", ShellSection.Accounts)]
    [DataRow("Ctrl+,", ShellSection.Settings)]
    public async Task TryHandleShellInput_Shortcut_NavigatesToExpectedSection(string shortcut, ShellSection expectedSection)
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        using var viewModel = new DesktopShellWindowViewModel(session);

        var handled = DesktopShellKeyRouting.TryHandleShellInput(viewModel, shortcut, shouldClosePalette: false);

        Assert.IsTrue(handled);
        Assert.AreEqual(expectedSection, viewModel.CurrentSection);
    }

    [TestMethod]
    public async Task TryHandleShellInput_ControlKShortcut_TogglesCommandPalette()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        using var viewModel = new DesktopShellWindowViewModel(session);

        var opened = DesktopShellKeyRouting.TryHandleShellInput(viewModel, "Ctrl+K", shouldClosePalette: false);
        var closed = DesktopShellKeyRouting.TryHandleShellInput(viewModel, "Ctrl+K", shouldClosePalette: false);

        Assert.IsTrue(opened);
        Assert.IsTrue(closed);
        Assert.IsFalse(viewModel.IsCommandPaletteOpen);
    }

    [TestMethod]
    public async Task TryHandleShellInput_WithoutShortcutOrPaletteClose_DoesNothing()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        using var viewModel = new DesktopShellWindowViewModel(session);
        viewModel.NavigateTo(ShellSection.Library);

        var handled = DesktopShellKeyRouting.TryHandleShellInput(viewModel, shortcut: null, shouldClosePalette: false);

        Assert.IsFalse(handled);
        Assert.AreEqual(ShellSection.Library, viewModel.CurrentSection);
    }

    [TestMethod]
    public void TryHandleShellInput_NullViewModel_ReturnsFalse()
        => Assert.IsFalse(DesktopShellKeyRouting.TryHandleShellInput(null, "Ctrl+K", shouldClosePalette: false));

    [TestMethod]
    public async Task TryHandleShellInput_ClosePaletteRequestWithoutOpenPalette_FallsBackToShortcut()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        using var viewModel = new DesktopShellWindowViewModel(session);

        var handled = DesktopShellKeyRouting.TryHandleShellInput(viewModel, "Ctrl+4", shouldClosePalette: true);

        Assert.IsTrue(handled);
        Assert.AreEqual(ShellSection.Downloads, viewModel.CurrentSection);
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
