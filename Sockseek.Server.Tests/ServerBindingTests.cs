using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public class ServerBindingTests
{
    [TestMethod]
    public void ResolveListenUrl_DefaultsToLoopbackDaemonPort()
    {
        Assert.AreEqual("http://127.0.0.1:5030", ServerHost.ResolveListenUrl(null));
        Assert.AreEqual("http://127.0.0.1:5030", ServerHost.ResolveListenUrl("  "));
    }

    [TestMethod]
    public void ResolveListenUrl_PreservesExplicitUrl()
    {
        Assert.AreEqual("http://0.0.0.0:6123", ServerHost.ResolveListenUrl("http://0.0.0.0:6123"));
    }

    [TestMethod]
    public void ResolveListenUrl_UsesConfiguredUrlWhenExplicitUrlIsMissing()
    {
        Assert.AreEqual("http://127.0.0.1:0", ServerHost.ResolveListenUrl(null, "http://127.0.0.1:0"));
        Assert.AreEqual("http://127.0.0.1:0", ServerHost.ResolveListenUrl("  ", "http://127.0.0.1:0"));
    }

    [DataTestMethod]
    [DataRow("http://localhost:5030")]
    [DataRow("http://[::1]:5030")]
    public void ResolveListenUrl_AcceptsConfiguredLoopbackUrlWhenExplicitUrlIsMissing(string configuredUrl)
    {
        Assert.AreEqual(configuredUrl, ServerHost.ResolveListenUrl(null, configuredUrl));
        Assert.AreEqual(configuredUrl, ServerHost.ResolveListenUrl("  ", configuredUrl));
    }

    [DataTestMethod]
    [DataRow("http://0.0.0.0:6123")]
    [DataRow("http://192.168.1.25:6123")]
    [DataRow("not-a-url")]
    public void ResolveListenUrl_RejectsNonLoopbackConfiguredUrlWhenExplicitUrlIsMissing(string configuredUrl)
    {
        Assert.AreEqual(ServerHost.DefaultListenUrl, ServerHost.ResolveListenUrl(null, configuredUrl));
        Assert.AreEqual(ServerHost.DefaultListenUrl, ServerHost.ResolveListenUrl("  ", configuredUrl));
    }

    [TestMethod]
    public void ResolveListenUrl_ExplicitUrlWinsOverConfiguredUrl()
    {
        Assert.AreEqual(
            "http://127.0.0.1:7001",
            ServerHost.ResolveListenUrl("http://127.0.0.1:7001", "http://127.0.0.1:0"));
    }
}
