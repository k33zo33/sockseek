using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopBackendRecoveryCoordinatorTests
{
    [TestMethod]
    public async Task Constructor_WithConnectedSupervisorSnapshot_StartsAndSubscribesInitialConnection()
    {
        var supervisor = new DesktopDaemonSupervisor();
        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"token-1\"}");
        var createdConnections = new List<FakeDesktopEventHubConnection>();

        await using var coordinator = new DesktopBackendRecoveryCoordinator(
            supervisor,
            handshake =>
            {
                var connection = new FakeDesktopEventHubConnection(handshake);
                createdConnections.Add(connection);
                return connection;
            });

        await coordinator.WhenIdleAsync();

        Assert.AreEqual(1, createdConnections.Count);
        Assert.AreEqual(1, createdConnections[0].StartCallCount);
        Assert.AreEqual(1, createdConnections[0].SubscribeAllCallCount);
        Assert.AreEqual(DesktopBackendEventsConnectionState.Connected, coordinator.EventsState);
    }

    [TestMethod]
    public async Task SupervisorRestarting_DisposesActiveManagerAndMarksDisconnected()
    {
        var supervisor = new DesktopDaemonSupervisor();
        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"token-1\"}");
        var createdConnections = new List<FakeDesktopEventHubConnection>();

        await using var coordinator = new DesktopBackendRecoveryCoordinator(
            supervisor,
            handshake =>
            {
                var connection = new FakeDesktopEventHubConnection(handshake);
                createdConnections.Add(connection);
                return connection;
            });

        await coordinator.WhenIdleAsync();
        supervisor.MarkRestarting();
        await coordinator.WhenIdleAsync();

        Assert.AreEqual(DesktopBackendEventsConnectionState.Disconnected, coordinator.EventsState);
        Assert.IsTrue(createdConnections[0].Disposed);
    }

    [TestMethod]
    public async Task SupervisorReconnectWithNewHandshake_CreatesFreshConnectionAndResubscribes()
    {
        var supervisor = new DesktopDaemonSupervisor();
        var createdConnections = new List<FakeDesktopEventHubConnection>();
        await using var coordinator = new DesktopBackendRecoveryCoordinator(
            supervisor,
            handshake =>
            {
                var connection = new FakeDesktopEventHubConnection(handshake);
                createdConnections.Add(connection);
                return connection;
            });

        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"token-1\"}");
        await coordinator.WhenIdleAsync();
        supervisor.MarkRestarting();
        await coordinator.WhenIdleAsync();
        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://127.0.0.1:5040\",\"SessionToken\":\"token-2\"}");
        await coordinator.WhenIdleAsync();

        Assert.AreEqual(2, createdConnections.Count);
        Assert.IsTrue(createdConnections[0].Disposed);
        Assert.AreEqual("token-2", createdConnections[1].Handshake.SessionToken);
        Assert.AreEqual(1, createdConnections[1].StartCallCount);
        Assert.AreEqual(1, createdConnections[1].SubscribeAllCallCount);
        Assert.AreEqual(DesktopBackendEventsConnectionState.Connected, coordinator.EventsState);
    }

    [TestMethod]
    public async Task DuplicateConnectedSnapshot_WithSameHandshake_DoesNotRecreateConnection()
    {
        var supervisor = new DesktopDaemonSupervisor();
        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"token-1\"}");
        var createdConnections = new List<FakeDesktopEventHubConnection>();

        await using var coordinator = new DesktopBackendRecoveryCoordinator(
            supervisor,
            handshake =>
            {
                var connection = new FakeDesktopEventHubConnection(handshake);
                createdConnections.Add(connection);
                return connection;
            });

        await coordinator.WhenIdleAsync();
        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"token-1\"}");
        await coordinator.WhenIdleAsync();

        Assert.AreEqual(1, createdConnections.Count);
        Assert.IsFalse(createdConnections[0].Disposed);
    }

    private sealed class FakeDesktopEventHubConnection(DesktopDaemonHandshake handshake) : IDesktopEventHubConnection
    {
        public DesktopDaemonHandshake Handshake { get; } = handshake;

        public event Func<Exception?, Task>? Reconnecting
        {
            add { }
            remove { }
        }

        public event Func<string?, Task>? Reconnected
        {
            add { }
            remove { }
        }

        public event Func<Exception?, Task>? Closed
        {
            add { }
            remove { }
        }

        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }
        public int SubscribeAllCallCount { get; private set; }
        public bool Disposed { get; private set; }

        public void OnServerEvent(Func<Sockseek.Api.ServerEventEnvelopeDto, Task> handler)
            => _ = handler;

        public void OnWorkflowUpdateBatch(Func<Sockseek.Api.WorkflowUpdateBatchDto, Task> handler)
            => _ = handler;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCallCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCallCount++;
            return Task.CompletedTask;
        }

        public Task SubscribeAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SubscribeAllCallCount++;
            return Task.CompletedTask;
        }

        public Task SubscribeWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
