using Microsoft.AspNetCore.SignalR.Client;
using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class SignalRDesktopEventHubConnection(HubConnection connection) : IDesktopEventHubConnection
{
    public event Func<Exception?, Task>? Reconnecting
    {
        add => connection.Reconnecting += value;
        remove => connection.Reconnecting -= value;
    }

    public event Func<string?, Task>? Reconnected
    {
        add => connection.Reconnected += value;
        remove => connection.Reconnected -= value;
    }

    public event Func<Exception?, Task>? Closed
    {
        add => connection.Closed += value;
        remove => connection.Closed -= value;
    }

    public void OnServerEvent(Func<ServerEventEnvelopeDto, Task> handler)
        => connection.On("serverEvent", handler);

    public void OnWorkflowUpdateBatch(Func<WorkflowUpdateBatchDto, Task> handler)
        => connection.On("workflowUpdateBatch", handler);

    public Task StartAsync(CancellationToken cancellationToken = default)
        => connection.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default)
        => connection.StopAsync(cancellationToken);

    public Task SubscribeAllAsync(CancellationToken cancellationToken = default)
        => connection.InvokeAsync("SubscribeAll", cancellationToken);

    public Task SubscribeWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
        => connection.InvokeAsync("SubscribeWorkflow", workflowId, cancellationToken);

    public ValueTask DisposeAsync()
        => connection.DisposeAsync();
}
