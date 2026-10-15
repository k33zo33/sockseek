using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopDaemonLaunchRequestFactoryTests
{
    [TestMethod]
    public void Create_WhenPackagedDaemonExists_UsesBundledExecutableAndLoopbackHandshake()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var daemonDirectory = Path.Combine(tempRoot, DesktopDaemonLaunchRequestFactory.PackagedDaemonDirectoryName);
            Directory.CreateDirectory(daemonDirectory);
            var executableName = OperatingSystem.IsWindows() ? "Sockseek.Server.exe" : "Sockseek.Server";
            var executablePath = Path.Combine(daemonDirectory, executableName);
            File.WriteAllText(executablePath, "packaged daemon fixture");
            var configDir = Path.Combine(tempRoot, "config");

            var request = DesktopDaemonLaunchRequestFactory.Create(
                "/workspace/sockseek",
                tempRoot,
                configDir);

            Assert.AreEqual(executablePath, request.FileName);
            Assert.AreEqual("--urls http://127.0.0.1:0", request.Arguments);
            Assert.AreEqual(daemonDirectory, request.WorkingDirectory);
            Assert.AreEqual(
                "1",
                request.EnvironmentVariables[DesktopDevelopmentDaemonLaunchRequestFactory.HandshakeStdoutEnvironmentVariable]);
            Assert.AreEqual(Path.GetFullPath(configDir), request.EnvironmentVariables["SockseekServer__ConfigDir"]);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Create_WhenPackagedDaemonIsMissing_UsesDevelopmentRequest()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var request = DesktopDaemonLaunchRequestFactory.Create("/workspace/sockseek", tempRoot);

            Assert.AreEqual(DesktopDevelopmentDaemonLaunchRequestFactory.DefaultDotnetExecutable, request.FileName);
            Assert.AreEqual(DesktopDevelopmentDaemonLaunchRequestFactory.DefaultArguments, request.Arguments);
            Assert.AreEqual("/workspace/sockseek", request.WorkingDirectory);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Create_WhenApplicationBaseDirectoryIsEmpty_ThrowsArgumentException()
    {
        var exception = Assert.ThrowsException<ArgumentException>(() =>
            DesktopDaemonLaunchRequestFactory.Create("/workspace/sockseek", " "));

        Assert.AreEqual("applicationBaseDirectory", exception.ParamName);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "sockseek-packaged-daemon-factory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
