namespace Sockseek.Desktop;

public interface IDesktopShellSession : IAsyncDisposable
{
    ShellNavigationViewModel Shell { get; }
    DesktopBackendEventsConnectionState EventsState { get; }
    event EventHandler<DesktopBackendEventsConnectionState>? EventsStateChanged;
    bool CanStartDaemon { get; }
    Task<bool> StartAsync(CancellationToken cancellationToken = default);
}
