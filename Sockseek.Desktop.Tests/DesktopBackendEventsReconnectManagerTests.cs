using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;

namespace Sockseek.Desktop.Tests;

[TestClass]
public class DesktopBackendEventsReconnectManagerTests
{
    [TestMethod]
    public async Task StartAsync_TransitionsToConnected_AndDelegatesStart()
    {
        var connection = new FakeDesktopEventHubConnection();
        await using var manager = new DesktopBackendEventsReconnectManager(connection);
        var states = new List<DesktopBackendEventsConnectionState>();
        manager.StateChanged += (_, state) => states.Add(state);

        await manager.StartAsync();

        Assert.IsTrue(connection.StartCalled);
        Assert.AreEqual(DesktopBackendEventsConnectionState.Connected, manager.State);
        CollectionAssert.AreEqual(
            new[]
            {
                DesktopBackendEventsConnectionState.Connecting,
                DesktopBackendEventsConnectionState.Connected,
            },
            states);
    }

    [TestMethod]
    public async Task StopAsync_TransitionsToDisconnected_AndDelegatesStop()
    {
        var connection = new FakeDesktopEventHubConnection();
        await using var manager = new DesktopBackendEventsReconnectManager(connection);
        await manager.StartAsync();

        await manager.StopAsync();

        Assert.IsTrue(connection.StopCalled);
        Assert.AreEqual(DesktopBackendEventsConnectionState.Disconnected, manager.State);
    }

    [TestMethod]
    public async Task SubscribeMethods_DelegateToUnderlyingConnection()
    {
        var connection = new FakeDesktopEventHubConnection();
        await using var manager = new DesktopBackendEventsReconnectManager(connection);
        var workflowId = Guid.NewGuid();

        await manager.SubscribeAllAsync();
        await manager.SubscribeWorkflowAsync(workflowId);

        Assert.IsTrue(connection.SubscribeAllCalled);
        Assert.AreEqual(workflowId, connection.SubscribedWorkflowId);
    }

    [TestMethod]
    public async Task ConnectionLifecycleCallbacks_UpdateManagerState()
    {
        var connection = new FakeDesktopEventHubConnection();
        await using var manager = new DesktopBackendEventsReconnectManager(connection);
        await manager.StartAsync();

        await connection.RaiseReconnectingAsync();
        Assert.AreEqual(DesktopBackendEventsConnectionState.Reconnecting, manager.State);

        await connection.RaiseReconnectedAsync();
        Assert.AreEqual(DesktopBackendEventsConnectionState.Connected, manager.State);

        await connection.RaiseClosedAsync();
        Assert.AreEqual(DesktopBackendEventsConnectionState.Disconnected, manager.State);
    }

    [TestMethod]
    public async Task ServerEventCallback_RehydratesTypedPayload()
    {
        var connection = new FakeDesktopEventHubConnection();
        await using var manager = new DesktopBackendEventsReconnectManager(connection);
        ServerEventEnvelopeDto? received = null;
        manager.ServerEventReceived += (_, envelope) => received = envelope;

        await connection.EmitServerEventAsync(new ServerEventEnvelopeDto(
            1,
            "search.updated",
            DateTimeOffset.UtcNow,
            "state",
            true,
            Guid.NewGuid(),
            JsonSerializer.SerializeToElement(new SearchUpdatedDto(Guid.NewGuid(), Guid.NewGuid(), 2, 12, true), SockseekApiJson.CreateSerializerOptions())));

        Assert.IsNotNull(received);
        Assert.IsInstanceOfType<SearchUpdatedDto>(received.Payload);
        Assert.AreEqual(12, ((SearchUpdatedDto)received.Payload).ResultCount);
    }

    [TestMethod]
    public async Task WorkflowBatchCallback_RehydratesActivityPayloads()
    {
        var connection = new FakeDesktopEventHubConnection();
        await using var manager = new DesktopBackendEventsReconnectManager(connection);
        WorkflowUpdateBatchDto? received = null;
        manager.WorkflowUpdateBatchReceived += (_, batch) => received = batch;

        var workflowId = Guid.NewGuid();
        var summary = new WorkflowSummaryDto(workflowId, "Test", ServerWorkflowState.Active, [], 1, 0, 0);
        var activity = new ServerEventEnvelopeDto(
            2,
            "search.updated",
            DateTimeOffset.UtcNow,
            "state",
            true,
            workflowId,
            JsonSerializer.SerializeToElement(new SearchUpdatedDto(Guid.NewGuid(), workflowId, 3, 8, false), SockseekApiJson.CreateSerializerOptions()));

        await connection.EmitWorkflowBatchAsync(new WorkflowUpdateBatchDto(
            2,
            DateTimeOffset.UtcNow,
            workflowId,
            summary,
            [],
            [],
            [],
            [activity]));

        Assert.IsNotNull(received);
        Assert.AreEqual(workflowId, received.WorkflowId);
        Assert.IsInstanceOfType<SearchUpdatedDto>(received.Activity.Single().Payload);
    }

    private sealed class FakeDesktopEventHubConnection : IDesktopEventHubConnection
    {
        private Func<ServerEventEnvelopeDto, Task>? serverEventHandler;
        private Func<WorkflowUpdateBatchDto, Task>? workflowBatchHandler;

        public event Func<Exception?, Task>? Reconnecting;
        public event Func<string?, Task>? Reconnected;
        public event Func<Exception?, Task>? Closed;

        public bool StartCalled { get; private set; }
        public bool StopCalled { get; private set; }
        public bool SubscribeAllCalled { get; private set; }
        public Guid? SubscribedWorkflowId { get; private set; }

        public void OnServerEvent(Func<ServerEventEnvelopeDto, Task> handler)
            => serverEventHandler = handler;

        public void OnWorkflowUpdateBatch(Func<WorkflowUpdateBatchDto, Task> handler)
            => workflowBatchHandler = handler;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalled = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCalled = true;
            return Task.CompletedTask;
        }

        public Task SubscribeAllAsync(CancellationToken cancellationToken = default)
        {
            SubscribeAllCalled = true;
            return Task.CompletedTask;
        }

        public Task SubscribeWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
        {
            SubscribedWorkflowId = workflowId;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task RaiseReconnectingAsync()
            => Reconnecting?.Invoke(null) ?? Task.CompletedTask;

        public Task RaiseReconnectedAsync()
            => Reconnected?.Invoke("connection-1") ?? Task.CompletedTask;

        public Task RaiseClosedAsync()
            => Closed?.Invoke(null) ?? Task.CompletedTask;

        public Task EmitServerEventAsync(ServerEventEnvelopeDto envelope)
            => serverEventHandler?.Invoke(envelope) ?? Task.CompletedTask;

        public Task EmitWorkflowBatchAsync(WorkflowUpdateBatchDto batch)
            => workflowBatchHandler?.Invoke(batch) ?? Task.CompletedTask;
    }
}
