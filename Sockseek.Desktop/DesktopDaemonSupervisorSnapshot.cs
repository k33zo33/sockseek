namespace Sockseek.Desktop;

public sealed record DesktopDaemonSupervisorSnapshot(
    BackendConnectionState State,
    DesktopDaemonHandshake? Handshake);
