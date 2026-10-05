namespace Sockseek.Desktop;

public sealed class DesktopDaemonSupervisor : IAsyncDisposable
{
    private static readonly TimeSpan ShutdownRequestTimeout = TimeSpan.FromSeconds(3);

    public event EventHandler<DesktopDaemonSupervisorSnapshot>? SnapshotChanged;

    private readonly IDesktopProcessLauncher? processLauncher;
    private readonly Func<DesktopDaemonHandshake, CancellationToken, Task> shutdownRequester;
    private IDesktopProcessSession? activeSession;

    public DesktopDaemonSupervisor(
        IDesktopProcessLauncher? processLauncher = null,
        Func<DesktopDaemonHandshake, CancellationToken, Task>? shutdownRequester = null)
    {
        this.processLauncher = processLauncher;
        this.shutdownRequester = shutdownRequester ?? RequestDaemonShutdownAsync;
    }

    public BackendConnectionState State { get; private set; } = BackendConnectionState.Starting;

    public DesktopDaemonHandshake? CurrentHandshake { get; private set; }

    public DesktopDaemonSupervisorSnapshot CurrentSnapshot => new(State, CurrentHandshake);

    public bool CanLaunch => processLauncher is not null;

    public async Task<bool> TryLaunchAsync(DesktopDaemonLaunchRequest request, CancellationToken cancellationToken = default)
    {
        if (processLauncher is null)
            return false;

        var previousHandshake = CurrentHandshake;
        ResetToStarting();
        await DisposeActiveSessionAsync(previousHandshake);

        var session = await processLauncher.LaunchAsync(request, cancellationToken);
        var handshake = await DesktopDaemonStartupParser.WaitForHandshakeAsync(session.ReadOutputLinesAsync(cancellationToken), cancellationToken);
        if (handshake is null)
        {
            await session.DisposeAsync();
            MarkDisconnected();
            return false;
        }

        activeSession = session;
        CurrentHandshake = handshake;
        State = BackendConnectionState.Connected;
        OnSnapshotChanged();
        return true;
    }

    public bool TryAcceptHandshakePayload(string payload)
    {
        if (!DesktopDaemonHandshake.TryParse(payload, out var handshake) || handshake is null)
            return false;

        CurrentHandshake = handshake;
        State = BackendConnectionState.Connected;
        OnSnapshotChanged();
        return true;
    }

    public void MarkRestarting()
    {
        CurrentHandshake = null;
        State = BackendConnectionState.Restarting;
        OnSnapshotChanged();
    }

    public void MarkDisconnected()
    {
        CurrentHandshake = null;
        State = BackendConnectionState.Disconnected;
        OnSnapshotChanged();
    }

    public void MarkUnauthorized()
    {
        CurrentHandshake = null;
        State = BackendConnectionState.Unauthorized;
        OnSnapshotChanged();
    }

    public void ResetToStarting()
    {
        CurrentHandshake = null;
        State = BackendConnectionState.Starting;
        OnSnapshotChanged();
    }

    public async ValueTask DisposeAsync()
        => await DisposeActiveSessionAsync(CurrentHandshake);

    private async ValueTask DisposeActiveSessionAsync(DesktopDaemonHandshake? handshake)
    {
        if (activeSession is null)
            return;

        var session = activeSession;
        activeSession = null;
        CurrentHandshake = null;
        await TryRequestDaemonShutdownAsync(handshake);
        await session.DisposeAsync();
    }

    private async Task TryRequestDaemonShutdownAsync(DesktopDaemonHandshake? handshake)
    {
        if (handshake is null)
            return;

        try
        {
            using var timeout = new CancellationTokenSource(ShutdownRequestTimeout);
            await shutdownRequester(handshake, timeout.Token);
        }
        catch
        {
            // Process disposal remains the final cleanup path if the daemon has already crashed or is unreachable.
        }
    }

    private static async Task RequestDaemonShutdownAsync(DesktopDaemonHandshake handshake, CancellationToken cancellationToken)
    {
        using var http = DesktopBackendClientFactory.CreateHttpClient(handshake);
        var client = new Sockseek.Api.SockseekApiClient(http);
        await client.RequestShutdownAsync(cancellationToken);
    }

    private void OnSnapshotChanged()
        => SnapshotChanged?.Invoke(this, CurrentSnapshot);
}
