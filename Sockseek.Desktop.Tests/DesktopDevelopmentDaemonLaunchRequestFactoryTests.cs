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
        Assert.AreEqual(
            "run --project Sockseek.Server/Sockseek.Server.csproj --no-launch-profile --urls http://127.0.0.1:0",
            request.Arguments);
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
    public void Create_UsesEnvironmentConfigDirWhenExplicitConfigDirIsMissing()
    {
        var previous = Environment.GetEnvironmentVariable(DesktopDevelopmentDaemonLaunchRequestFactory.ConfigDirEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                DesktopDevelopmentDaemonLaunchRequestFactory.ConfigDirEnvironmentVariable,
                "trace-config");

            var request = DesktopDevelopmentDaemonLaunchRequestFactory.Create("/workspace/sockseek");

            Assert.AreEqual(
                Path.GetFullPath("trace-config"),
                request.EnvironmentVariables["SockseekServer__ConfigDir"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DesktopDevelopmentDaemonLaunchRequestFactory.ConfigDirEnvironmentVariable,
                previous);
        }
    }

    [TestMethod]
    public void Create_ExplicitConfigDirOverridesEnvironmentConfigDir()
    {
        var previous = Environment.GetEnvironmentVariable(DesktopDevelopmentDaemonLaunchRequestFactory.ConfigDirEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                DesktopDevelopmentDaemonLaunchRequestFactory.ConfigDirEnvironmentVariable,
                "trace-config-env");

            var request = DesktopDevelopmentDaemonLaunchRequestFactory.Create(
                "/workspace/sockseek",
                configDir: "trace-config-explicit");

            Assert.AreEqual(
                Path.GetFullPath("trace-config-explicit"),
                request.EnvironmentVariables["SockseekServer__ConfigDir"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DesktopDevelopmentDaemonLaunchRequestFactory.ConfigDirEnvironmentVariable,
                previous);
        }
    }

    [TestMethod]
    public void Create_EmptyWorkspaceRoot_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(() => DesktopDevelopmentDaemonLaunchRequestFactory.Create(" "));
    }
}
