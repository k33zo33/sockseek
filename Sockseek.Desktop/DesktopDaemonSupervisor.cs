namespace Sockseek.Desktop;

public sealed class DesktopDaemonSupervisor
{
    public event EventHandler<DesktopDaemonSupervisorSnapshot>? SnapshotChanged;

    public BackendConnectionState State { get; private set; } = BackendConnectionState.Starting;

    public DesktopDaemonHandshake? CurrentHandshake { get; private set; }

    public DesktopDaemonSupervisorSnapshot CurrentSnapshot => new(State, CurrentHandshake);

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
