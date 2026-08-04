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
}
