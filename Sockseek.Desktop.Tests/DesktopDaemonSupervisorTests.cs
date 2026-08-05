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
}
