using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopShellDiagnosticsSnapshotTests
{
    [TestMethod]
    public async Task CreateDiagnosticsSnapshot_WhenConnected_IncludesSafeBackendSummaryWithoutSessionToken()
    {
        var supervisor = new DesktopDaemonSupervisor();
        await using var session = new DesktopShellSession(
            supervisor: supervisor,
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        var viewModel = new DesktopShellWindowViewModel(session);

        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"secret-token\"}");

        var snapshot = viewModel.CreateDiagnosticsSnapshot();
        var text = viewModel.CreateDiagnosticsText();

        Assert.AreEqual("Sockseek — Home", snapshot.WindowTitle);
        Assert.AreEqual("Home", snapshot.CurrentPageTitle);
        Assert.AreEqual(BackendConnectionState.Connected.ToString(), snapshot.BackendState);
        Assert.IsTrue(snapshot.HasHandshake);
        Assert.AreEqual("http://127.0.0.1:5030", snapshot.BackendBaseUrl);
        StringAssert.Contains(text, "Backend URL: http://127.0.0.1:5030");
        Assert.IsFalse(text.Contains("secret-token", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CreateDiagnosticsSnapshot_WhenDisconnected_UsesUnavailableBackendUrl()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        var viewModel = new DesktopShellWindowViewModel(session);

        session.Shell.SetBackendState(BackendConnectionState.Disconnected);
        var snapshot = viewModel.CreateDiagnosticsSnapshot();
        var text = snapshot.ToDisplayText();

        Assert.IsFalse(snapshot.HasHandshake);
        Assert.IsNull(snapshot.BackendBaseUrl);
        StringAssert.Contains(text, "Backend URL: unavailable");
        StringAssert.Contains(text, "Backend state: Disconnected");
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

        public void OnServerEvent(Func<Sockseek.Api.ServerEventEnvelopeDto, Task> handler)
            => _ = handler;

        public void OnWorkflowUpdateBatch(Func<Sockseek.Api.WorkflowUpdateBatchDto, Task> handler)
            => _ = handler;

        public Task StartAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SubscribeAllAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SubscribeWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
