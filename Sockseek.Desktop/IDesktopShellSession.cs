namespace Sockseek.Desktop;

public interface IDesktopShellSession : IAsyncDisposable
{
    ShellNavigationViewModel Shell { get; }
    DesktopBackendEventsConnectionState EventsState { get; }
    event EventHandler<DesktopBackendEventsConnectionState>? EventsStateChanged;
    event EventHandler<Sockseek.Api.WorkflowUpdateBatchDto>? WorkflowUpdateBatchReceived;
    bool CanStartDaemon { get; }
    Task<bool> StartAsync(CancellationToken cancellationToken = default);
}
