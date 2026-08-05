using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public class DesktopDaemonSupervisorTests
{
    private const string ValidLoopbackPayload = "{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"local-session-token\"}";
    private const string LocalhostPayload = "{\"BaseUrl\":\"http://localhost:5030\",\"SessionToken\":\"token\"}";

    [TestMethod]
    public void TryAcceptHandshakePayload_ValidLoopbackPayload_TransitionsToConnected()
    {
        var supervisor = new DesktopDaemonSupervisor();

        var accepted = supervisor.TryAcceptHandshakePayload(ValidLoopbackPayload);

        Assert.IsTrue(accepted);
        Assert.AreEqual(BackendConnectionState.Connected, supervisor.State);
        Assert.IsNotNull(supervisor.CurrentHandshake);
        Assert.AreEqual("http://127.0.0.1:5030", supervisor.CurrentHandshake.BaseUrl);
        Assert.AreEqual("local-session-token", supervisor.CurrentHandshake.SessionToken);
    }

    [TestMethod]
    public void TryAcceptHandshakePayload_InvalidPayload_DoesNotChangeState()
    {
        foreach (var payload in new[]
                 {
                     string.Empty,
                     "not-json",
                     "{}",
                     "{\"BaseUrl\":\"https://example.com:5030\",\"SessionToken\":\"token\"}",
                     "{\"BaseUrl\":\"http://192.168.1.10:5030\",\"SessionToken\":\"token\"}",
                     "{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"   \"}"
                 })
        {
            var supervisor = new DesktopDaemonSupervisor();

            var accepted = supervisor.TryAcceptHandshakePayload(payload);

            Assert.IsFalse(accepted);
            Assert.AreEqual(BackendConnectionState.Starting, supervisor.State);
            Assert.IsNull(supervisor.CurrentHandshake);
        }
    }

    [TestMethod]
    public void MarkRestarting_ClearsHandshakeAndTransitionsState()
    {
        var supervisor = new DesktopDaemonSupervisor();
        supervisor.TryAcceptHandshakePayload(LocalhostPayload);

        supervisor.MarkRestarting();

        Assert.AreEqual(BackendConnectionState.Restarting, supervisor.State);
        Assert.IsNull(supervisor.CurrentHandshake);
    }

    [TestMethod]
    public void MarkDisconnected_ClearsHandshakeAndTransitionsState()
    {
        var supervisor = new DesktopDaemonSupervisor();
        supervisor.TryAcceptHandshakePayload(LocalhostPayload);

        supervisor.MarkDisconnected();

        Assert.AreEqual(BackendConnectionState.Disconnected, supervisor.State);
        Assert.IsNull(supervisor.CurrentHandshake);
    }

    [TestMethod]
    public void MarkUnauthorized_ClearsHandshakeAndTransitionsState()
    {
        var supervisor = new DesktopDaemonSupervisor();
        supervisor.TryAcceptHandshakePayload(LocalhostPayload);

        supervisor.MarkUnauthorized();

        Assert.AreEqual(BackendConnectionState.Unauthorized, supervisor.State);
        Assert.IsNull(supervisor.CurrentHandshake);
    }

    [TestMethod]
    public void ResetToStarting_ClearsHandshakeAndReturnsToStarting()
    {
        var supervisor = new DesktopDaemonSupervisor();
        supervisor.TryAcceptHandshakePayload(LocalhostPayload);

        supervisor.ResetToStarting();

        Assert.AreEqual(BackendConnectionState.Starting, supervisor.State);
        Assert.IsNull(supervisor.CurrentHandshake);
    }

    [TestMethod]
    public void StateChanges_RaiseSnapshotChangedEvent()
    {
        var supervisor = new DesktopDaemonSupervisor();
        var snapshots = new List<DesktopDaemonSupervisorSnapshot>();
        supervisor.SnapshotChanged += (_, snapshot) => snapshots.Add(snapshot);

        supervisor.TryAcceptHandshakePayload(ValidLoopbackPayload);
        supervisor.MarkRestarting();
        supervisor.MarkDisconnected();

        Assert.AreEqual(3, snapshots.Count);
        Assert.AreEqual(BackendConnectionState.Connected, snapshots[0].State);
        Assert.IsNotNull(snapshots[0].Handshake);
        Assert.AreEqual(BackendConnectionState.Restarting, snapshots[1].State);
        Assert.IsNull(snapshots[1].Handshake);
        Assert.AreEqual(BackendConnectionState.Disconnected, snapshots[2].State);
        Assert.IsNull(snapshots[2].Handshake);
    }

    [TestMethod]
    public async Task TryLaunchAsync_ValidHandshakeOutput_TransitionsToConnected()
    {
        var launcher = new FakeProcessLauncher([
            "booting",
            "SOCKSEEK_DAEMON_HANDSHAKE={\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"launch-token\"}"
        ]);
        var supervisor = new DesktopDaemonSupervisor(launcher);

        var launched = await supervisor.TryLaunchAsync(new DesktopDaemonLaunchRequest(
            "dotnet",
            "run --project Sockseek.Server",
            "/tmp",
            new Dictionary<string, string?>()));

        Assert.IsTrue(launched);
        Assert.AreEqual(BackendConnectionState.Connected, supervisor.State);
        Assert.IsNotNull(supervisor.CurrentHandshake);
        Assert.AreEqual("launch-token", supervisor.CurrentHandshake.SessionToken);
        Assert.AreEqual("dotnet", launcher.LastRequest?.FileName);
    }

    [TestMethod]
    public async Task TryLaunchAsync_NoHandshakeOutput_TransitionsToDisconnected()
    {
        var launcher = new FakeProcessLauncher(["booting", "still booting"]);
        var supervisor = new DesktopDaemonSupervisor(launcher);

        var launched = await supervisor.TryLaunchAsync(new DesktopDaemonLaunchRequest(
            "dotnet",
            "run",
            "/tmp",
            new Dictionary<string, string?>()));

        Assert.IsFalse(launched);
        Assert.AreEqual(BackendConnectionState.Disconnected, supervisor.State);
        Assert.IsNull(supervisor.CurrentHandshake);
    }

    private sealed class FakeProcessLauncher(params string[] outputLines) : IDesktopProcessLauncher
    {
        public DesktopDaemonLaunchRequest? LastRequest { get; private set; }

        public Task<IDesktopProcessSession> LaunchAsync(DesktopDaemonLaunchRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult<IDesktopProcessSession>(new FakeProcessSession(outputLines));
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
