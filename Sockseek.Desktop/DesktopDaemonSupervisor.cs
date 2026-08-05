namespace Sockseek.Desktop;

public sealed class DesktopDaemonSupervisor
{
    public BackendConnectionState State { get; private set; } = BackendConnectionState.Starting;

    public DesktopDaemonHandshake? CurrentHandshake { get; private set; }

    public bool TryAcceptHandshakePayload(string payload)
    {
        if (!DesktopDaemonHandshake.TryParse(payload, out var handshake) || handshake is null)
            return false;

        CurrentHandshake = handshake;
        State = BackendConnectionState.Connected;
        return true;
    }

    public void MarkRestarting()
    {
        CurrentHandshake = null;
        State = BackendConnectionState.Restarting;
    }

    public void MarkDisconnected()
    {
        CurrentHandshake = null;
        State = BackendConnectionState.Disconnected;
    }

    public void MarkUnauthorized()
    {
        CurrentHandshake = null;
        State = BackendConnectionState.Unauthorized;
    }

    public void ResetToStarting()
    {
        CurrentHandshake = null;
        State = BackendConnectionState.Starting;
    }
}
