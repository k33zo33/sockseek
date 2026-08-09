namespace Sockseek.Desktop;

public sealed class DesktopBackendRecoveryCoordinator : IAsyncDisposable
{
    private readonly DesktopDaemonSupervisor supervisor;
    private readonly Func<DesktopDaemonHandshake, IDesktopEventHubConnection> connectionFactory;
    private readonly SemaphoreSlim transitionLock = new(1, 1);
    private DesktopBackendEventsReconnectManager? activeManager;
    private DesktopDaemonHandshake? activeHandshake;
    private Task transitionTask = Task.CompletedTask;
    private bool disposed;

    public DesktopBackendRecoveryCoordinator(
        DesktopDaemonSupervisor supervisor,
        Func<DesktopDaemonHandshake, IDesktopEventHubConnection>? connectionFactory = null)
    {
        this.supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        this.connectionFactory = connectionFactory ?? DesktopBackendEventsConnectionFactory.Create;
        supervisor.SnapshotChanged += HandleSupervisorSnapshotChanged;
        QueueSnapshot(supervisor.CurrentSnapshot);
    }

    public DesktopBackendEventsConnectionState EventsState { get; private set; } = DesktopBackendEventsConnectionState.Disconnected;

    public event EventHandler<DesktopBackendEventsConnectionState>? EventsStateChanged;

    public Task WhenIdleAsync() => transitionTask;

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        disposed = true;
        supervisor.SnapshotChanged -= HandleSupervisorSnapshotChanged;
        await AwaitTransitionCompletionAsync().ConfigureAwait(false);
        await DisposeActiveManagerAsync().ConfigureAwait(false);
        transitionLock.Dispose();
    }

    private void HandleSupervisorSnapshotChanged(object? sender, DesktopDaemonSupervisorSnapshot snapshot)
        => QueueSnapshot(snapshot);

    private void QueueSnapshot(DesktopDaemonSupervisorSnapshot snapshot)
    {
        transitionTask = transitionTask
            .ContinueWith(
                _ => ApplySnapshotAsync(snapshot),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default)
            .Unwrap();
    }

    private async Task ApplySnapshotAsync(DesktopDaemonSupervisorSnapshot snapshot)
    {
        await transitionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
                return;

            if (snapshot.State == BackendConnectionState.Connected && snapshot.Handshake is not null)
            {
                if (activeManager is not null && Equals(activeHandshake, snapshot.Handshake))
                    return;

                await DisposeActiveManagerAsync().ConfigureAwait(false);
                var manager = new DesktopBackendEventsReconnectManager(connectionFactory(snapshot.Handshake));
                manager.StateChanged += HandleManagerStateChanged;

                try
                {
                    await manager.StartAsync().ConfigureAwait(false);
                    await manager.SubscribeAllAsync().ConfigureAwait(false);
                    activeManager = manager;
                    activeHandshake = snapshot.Handshake;
                    SetEventsState(manager.State);
                }
                catch
                {
                    manager.StateChanged -= HandleManagerStateChanged;
                    await manager.DisposeAsync().ConfigureAwait(false);
                    activeHandshake = null;
                    SetEventsState(DesktopBackendEventsConnectionState.Disconnected);
                }

                return;
            }

            await DisposeActiveManagerAsync().ConfigureAwait(false);
        }
        finally
        {
            transitionLock.Release();
        }
    }

    private Task AwaitTransitionCompletionAsync()
        => transitionTask.ContinueWith(_ => Task.CompletedTask).Unwrap();

    private async ValueTask DisposeActiveManagerAsync()
    {
        if (activeManager is null)
        {
            activeHandshake = null;
            SetEventsState(DesktopBackendEventsConnectionState.Disconnected);
            return;
        }

        var manager = activeManager;
        activeManager = null;
        activeHandshake = null;
        manager.StateChanged -= HandleManagerStateChanged;
        await manager.DisposeAsync().ConfigureAwait(false);
        SetEventsState(DesktopBackendEventsConnectionState.Disconnected);
    }

    private void HandleManagerStateChanged(object? sender, DesktopBackendEventsConnectionState state)
        => SetEventsState(state);

    private void SetEventsState(DesktopBackendEventsConnectionState state)
    {
        if (EventsState == state)
            return;

        EventsState = state;
        EventsStateChanged?.Invoke(this, state);
    }
}
