using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public class DesktopDevelopmentDaemonLaunchRequestFactoryTests
{
    [TestMethod]
    public void Create_UsesExpectedDevelopmentDaemonDefaults()
    {
        var request = DesktopDevelopmentDaemonLaunchRequestFactory.Create("/workspace/sockseek");

        Assert.AreEqual("dotnet", request.FileName);
        Assert.AreEqual("run --project Sockseek.Server/Sockseek.Server.csproj --no-launch-profile", request.Arguments);
        Assert.AreEqual("/workspace/sockseek", request.WorkingDirectory);
        Assert.AreEqual("1", request.EnvironmentVariables[DesktopDevelopmentDaemonLaunchRequestFactory.HandshakeStdoutEnvironmentVariable]);
    }

    [TestMethod]
    public void Create_AllowsCustomDotnetExecutable()
    {
        var request = DesktopDevelopmentDaemonLaunchRequestFactory.Create("/workspace/sockseek", "/usr/local/bin/dotnet");

        Assert.AreEqual("/usr/local/bin/dotnet", request.FileName);
    }

    [TestMethod]
    public void Create_EmptyWorkspaceRoot_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(() => DesktopDevelopmentDaemonLaunchRequestFactory.Create(" "));
    }
}
