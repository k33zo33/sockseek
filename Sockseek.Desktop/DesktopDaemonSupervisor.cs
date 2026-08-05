namespace Sockseek.Desktop;

public sealed class DesktopDaemonSupervisor
{
    public event EventHandler<DesktopDaemonSupervisorSnapshot>? SnapshotChanged;

    private readonly IDesktopProcessLauncher? processLauncher;

    public DesktopDaemonSupervisor(IDesktopProcessLauncher? processLauncher = null)
        => this.processLauncher = processLauncher;

    public BackendConnectionState State { get; private set; } = BackendConnectionState.Starting;

    public DesktopDaemonHandshake? CurrentHandshake { get; private set; }

    public DesktopDaemonSupervisorSnapshot CurrentSnapshot => new(State, CurrentHandshake);

    public bool CanLaunch => processLauncher is not null;

    public async Task<bool> TryLaunchAsync(DesktopDaemonLaunchRequest request, CancellationToken cancellationToken = default)
    {
        if (processLauncher is null)
            return false;

        ResetToStarting();
        await using var session = await processLauncher.LaunchAsync(request, cancellationToken);
        var handshake = await DesktopDaemonStartupParser.WaitForHandshakeAsync(session.ReadOutputLinesAsync(cancellationToken), cancellationToken);
        if (handshake is null)
        {
            MarkDisconnected();
            return false;
        }

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

    private void OnSnapshotChanged()
        => SnapshotChanged?.Invoke(this, CurrentSnapshot);
}
