using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopBackendEventsReconnectManager : IAsyncDisposable
{
    private readonly IDesktopEventHubConnection connection;

    public DesktopBackendEventsReconnectManager(IDesktopEventHubConnection connection)
    {
        this.connection = connection;
        this.connection.Reconnecting += HandleReconnectingAsync;
        this.connection.Reconnected += HandleReconnectedAsync;
        this.connection.Closed += HandleClosedAsync;
        this.connection.OnServerEvent(HandleServerEventAsync);
        this.connection.OnWorkflowUpdateBatch(HandleWorkflowUpdateBatchAsync);
    }

    public event EventHandler<DesktopBackendEventsConnectionState>? StateChanged;
    public event EventHandler<ServerEventEnvelopeDto>? ServerEventReceived;
    public event EventHandler<WorkflowUpdateBatchDto>? WorkflowUpdateBatchReceived;

    public DesktopBackendEventsConnectionState State { get; private set; } = DesktopBackendEventsConnectionState.Disconnected;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        SetState(DesktopBackendEventsConnectionState.Connecting);
        await connection.StartAsync(cancellationToken);
        SetState(DesktopBackendEventsConnectionState.Connected);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await connection.StopAsync(cancellationToken);
        SetState(DesktopBackendEventsConnectionState.Disconnected);
    }

    public Task SubscribeAllAsync(CancellationToken cancellationToken = default)
        => connection.SubscribeAllAsync(cancellationToken);

    public Task SubscribeWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
        => connection.SubscribeWorkflowAsync(workflowId, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        connection.Reconnecting -= HandleReconnectingAsync;
        connection.Reconnected -= HandleReconnectedAsync;
        connection.Closed -= HandleClosedAsync;
        await connection.DisposeAsync();
        SetState(DesktopBackendEventsConnectionState.Disconnected);
    }

    private Task HandleReconnectingAsync(Exception? exception)
    {
        _ = exception;
        SetState(DesktopBackendEventsConnectionState.Reconnecting);
        return Task.CompletedTask;
    }

    private Task HandleReconnectedAsync(string? connectionId)
    {
        _ = connectionId;
        SetState(DesktopBackendEventsConnectionState.Connected);
        return Task.CompletedTask;
    }

    private Task HandleClosedAsync(Exception? exception)
    {
        _ = exception;
        SetState(DesktopBackendEventsConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    private Task HandleServerEventAsync(ServerEventEnvelopeDto envelope)
    {
        ServerEventReceived?.Invoke(this, ServerEventPayloadConverter.RehydrateEnvelope(envelope));
        return Task.CompletedTask;
    }

    private Task HandleWorkflowUpdateBatchAsync(WorkflowUpdateBatchDto batch)
    {
        WorkflowUpdateBatchReceived?.Invoke(this, ServerEventPayloadConverter.RehydrateBatch(batch));
        return Task.CompletedTask;
    }

    private void SetState(DesktopBackendEventsConnectionState state)
    {
        if (State == state)
            return;

        State = state;
        StateChanged?.Invoke(this, state);
    }
}
