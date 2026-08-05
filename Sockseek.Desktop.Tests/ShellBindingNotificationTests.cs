using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class ShellBindingNotificationTests
{
    [TestMethod]
    public void ShellNavigationViewModel_NavigateThemeAndBackendChanges_RaisePropertyChanged()
    {
        var viewModel = new ShellNavigationViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        viewModel.NavigateTo(ShellSection.Downloads);
        viewModel.SetTheme(DesktopThemePreference.Dark);
        viewModel.SetBackendState(BackendConnectionState.Restarting);

        CollectionAssert.Contains(changedProperties, nameof(ShellNavigationViewModel.CurrentSection));
        CollectionAssert.Contains(changedProperties, nameof(ShellNavigationViewModel.CurrentPage));
        CollectionAssert.Contains(changedProperties, nameof(ShellNavigationViewModel.CurrentTheme));
        CollectionAssert.Contains(changedProperties, nameof(ShellNavigationViewModel.BackendState));
        CollectionAssert.Contains(changedProperties, nameof(ShellNavigationViewModel.StatusBanner));
    }

    [TestMethod]
    public void CommandPalette_OpenCloseAndToggle_RaiseIsOpenPropertyChanged()
    {
        var viewModel = new CommandPaletteViewModel([]);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        viewModel.Open();
        viewModel.Close();
        viewModel.Toggle();

        Assert.AreEqual(3, changedProperties.Count(name => name == nameof(CommandPaletteViewModel.IsOpen)));
    }

    [TestMethod]
    public async Task DesktopShellWindowViewModel_ReflectsShellPropertyChanges()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        var viewModel = new DesktopShellWindowViewModel(session);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        session.Shell.NavigateTo(ShellSection.Settings);
        session.Shell.SetTheme(DesktopThemePreference.Dark);
        session.Shell.SetBackendState(BackendConnectionState.Disconnected);

        CollectionAssert.Contains(changedProperties, nameof(DesktopShellWindowViewModel.CurrentPage));
        CollectionAssert.Contains(changedProperties, nameof(DesktopShellWindowViewModel.WindowTitle));
        CollectionAssert.Contains(changedProperties, nameof(DesktopShellWindowViewModel.CurrentTheme));
        CollectionAssert.Contains(changedProperties, nameof(DesktopShellWindowViewModel.StatusBanner));
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
