using Sockseek.Api;

namespace Sockseek.Desktop;

public interface IDesktopEventHubConnection : IAsyncDisposable
{
    event Func<Exception?, Task>? Reconnecting;
    event Func<string?, Task>? Reconnected;
    event Func<Exception?, Task>? Closed;

    void OnServerEvent(Func<ServerEventEnvelopeDto, Task> handler);
    void OnWorkflowUpdateBatch(Func<WorkflowUpdateBatchDto, Task> handler);

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SubscribeAllAsync(CancellationToken cancellationToken = default);
    Task SubscribeWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default);
}
