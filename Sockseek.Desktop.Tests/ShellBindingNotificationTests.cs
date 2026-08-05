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
        session.Shell.OpenCommandPalette();
        session.Shell.SetBackendState(BackendConnectionState.Disconnected);

        CollectionAssert.Contains(changedProperties, nameof(DesktopShellWindowViewModel.CurrentPage));
        CollectionAssert.Contains(changedProperties, nameof(DesktopShellWindowViewModel.WindowTitle));
        CollectionAssert.Contains(changedProperties, nameof(DesktopShellWindowViewModel.CurrentTheme));
        CollectionAssert.Contains(changedProperties, nameof(DesktopShellWindowViewModel.IsCommandPaletteOpen));
        CollectionAssert.Contains(changedProperties, nameof(DesktopShellWindowViewModel.StatusBanner));
        CollectionAssert.Contains(changedProperties, nameof(DesktopShellWindowViewModel.CanStartDaemon));
    }

    [TestMethod]
    public async Task DesktopShellWindowViewModel_Dispose_StopsForwardingShellNotifications()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        var viewModel = new DesktopShellWindowViewModel(session);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        viewModel.Dispose();
        session.Shell.NavigateTo(ShellSection.Settings);
        session.Shell.OpenCommandPalette();
        session.Shell.SetBackendState(BackendConnectionState.Disconnected);

        Assert.AreEqual(0, changedProperties.Count);
    }

    [TestMethod]
    public async Task DesktopShellWindowViewModel_TryStartDaemonAsync_RaisesBusyStateNotifications()
    {
        var processSession = new ControllableProcessSession();
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(new ControlledProcessLauncher(processSession)),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake),
            workspaceRoot: "/workspace",
            launchRequestFactory: root => new DesktopDaemonLaunchRequest(
                "dotnet",
                "run --project Sockseek.Server/Sockseek.Server.csproj",
                root,
                new Dictionary<string, string?>()));
        var viewModel = new DesktopShellWindowViewModel(session);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);
        session.Shell.SetBackendState(BackendConnectionState.Disconnected);

        var startTask = viewModel.TryStartDaemonAsync();
        await processSession.WaitUntilReadStartedAsync();
        processSession.CompleteWith("SOCKSEEK_DAEMON_HANDSHAKE={\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"session-token-3\"}");

        await startTask;
        await session.RecoveryCoordinator.WhenIdleAsync();

        Assert.IsTrue(changedProperties.Count(name => name == nameof(DesktopShellWindowViewModel.IsStartingDaemon)) >= 2);
        Assert.IsTrue(changedProperties.Count(name => name == nameof(DesktopShellWindowViewModel.CanStartDaemon)) >= 3);
    }

    private sealed class ControlledProcessLauncher(ControllableProcessSession session) : IDesktopProcessLauncher
    {
        public Task<IDesktopProcessSession> LaunchAsync(DesktopDaemonLaunchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IDesktopProcessSession>(session);
    }

    private sealed class ControllableProcessSession : IDesktopProcessSession
    {
        private readonly TaskCompletionSource<bool> readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<string[]> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task WaitUntilReadStartedAsync() => readStarted.Task;

        public void CompleteWith(params string[] outputLines) => completion.TrySetResult(outputLines);

        public async IAsyncEnumerable<string> ReadOutputLinesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            readStarted.TrySetResult(true);
            var outputLines = await completion.Task.WaitAsync(cancellationToken);
            foreach (var line in outputLines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
                await Task.Yield();
            }
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
