using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopShellSessionTests
{
    [TestMethod]
    public async Task StartAsync_WithLaunchableSupervisor_StartsDaemonAndRecoveryFlow()
    {
        var launcher = new FakeProcessLauncher([
            "SOCKSEEK_DAEMON_HANDSHAKE={\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"session-token-1\"}"
        ]);
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(launcher),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake),
            workspaceRoot: "/workspace",
            launchRequestFactory: root => new DesktopDaemonLaunchRequest(
                "dotnet",
                "run --project Sockseek.Server/Sockseek.Server.csproj",
                root,
                new Dictionary<string, string?>()));

        var started = await session.StartAsync();
        await session.RecoveryCoordinator.WhenIdleAsync();

        Assert.IsTrue(started);
        Assert.AreEqual(BackendConnectionState.Connected, session.Shell.BackendState);
        Assert.AreEqual(DesktopBackendEventsConnectionState.Connected, session.RecoveryCoordinator.EventsState);
        Assert.AreEqual("session-token-1", session.Shell.CurrentHandshake?.SessionToken);
        Assert.AreEqual("/workspace", launcher.LastRequest?.WorkingDirectory);
    }

    [TestMethod]
    public async Task StartAsync_WithoutWorkspaceRoot_ReturnsFalse()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(new FakeProcessLauncher()),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));

        var started = await session.StartAsync();

        Assert.IsFalse(started);
        Assert.AreEqual(BackendConnectionState.Starting, session.Shell.BackendState);
        Assert.AreEqual(DesktopBackendEventsConnectionState.Disconnected, session.RecoveryCoordinator.EventsState);
    }

    [TestMethod]
    public async Task Session_ComposesSupervisorStateChanges_IntoShellAndRecoveryCoordinator()
    {
        var supervisor = new DesktopDaemonSupervisor();
        await using var session = new DesktopShellSession(
            supervisor: supervisor,
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));

        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"session-token-1\"}");
        await session.RecoveryCoordinator.WhenIdleAsync();
        Assert.AreEqual(BackendConnectionState.Connected, session.Shell.BackendState);
        Assert.AreEqual(DesktopBackendEventsConnectionState.Connected, session.RecoveryCoordinator.EventsState);

        supervisor.MarkRestarting();
        await session.RecoveryCoordinator.WhenIdleAsync();
        Assert.AreEqual(BackendConnectionState.Restarting, session.Shell.BackendState);
        Assert.AreEqual(DesktopBackendEventsConnectionState.Disconnected, session.RecoveryCoordinator.EventsState);
    }

    [TestMethod]
    public async Task Session_UsesProvidedThemePreferenceStore_ForCrossSessionPersistence()
    {
        var store = new InMemoryDesktopThemePreferenceStore(DesktopThemePreference.System);

        await using (var firstSession = new DesktopShellSession(
                         supervisor: new DesktopDaemonSupervisor(),
                         connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake),
                         themePreferenceStore: store))
        {
            firstSession.Shell.SetTheme(DesktopThemePreference.Dark);
        }

        await using var secondSession = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake),
            themePreferenceStore: store);

        Assert.AreEqual(DesktopThemePreference.Dark, secondSession.Shell.CurrentTheme);
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

    private sealed class FakeProcessLauncher(params string[][] sessionOutputs) : IDesktopProcessLauncher
    {
        private readonly Queue<string[]> sessionOutputs = new(sessionOutputs);

        public DesktopDaemonLaunchRequest? LastRequest { get; private set; }

        public Task<IDesktopProcessSession> LaunchAsync(DesktopDaemonLaunchRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var output = sessionOutputs.Count > 0 ? sessionOutputs.Dequeue() : [];
            return Task.FromResult<IDesktopProcessSession>(new FakeProcessSession(output));
        }
    }

    private sealed class FakeProcessSession(params string[] outputLines) : IDesktopProcessSession
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async IAsyncEnumerable<string> ReadOutputLinesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var line in outputLines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
                await Task.Yield();
            }
        }
    }
}
