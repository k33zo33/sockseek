using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public class DesktopBackendEventsConnectionIntegrationTests
{
    [TestMethod]
    public async Task ReconnectManager_StartSubscribeStop_WorksAgainstRealDaemon()
    {
        var workspaceRoot = FindWorkspaceRoot();
        var tempConfigDir = Path.Combine(Path.GetTempPath(), "Sockseek-desktop-events-" + Guid.NewGuid());
        Directory.CreateDirectory(tempConfigDir);
        try
        {
            var request = DesktopDevelopmentDaemonLaunchRequestFactory.Create(workspaceRoot, configDir: tempConfigDir);
            await using var supervisor = new DesktopDaemonSupervisor(new SystemDesktopProcessLauncher());
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            var launched = await supervisor.TryLaunchAsync(request, cts.Token);

            Assert.IsTrue(launched);
            Assert.IsNotNull(supervisor.CurrentHandshake);

            await using var manager = new DesktopBackendEventsReconnectManager(
                DesktopBackendEventsConnectionFactory.Create(supervisor.CurrentHandshake));

            await manager.StartAsync(cts.Token);
            await manager.SubscribeAllAsync(cts.Token);
            Assert.AreEqual(DesktopBackendEventsConnectionState.Connected, manager.State);

            await manager.StopAsync(cts.Token);
            Assert.AreEqual(DesktopBackendEventsConnectionState.Disconnected, manager.State);
        }
        finally
        {
            DeleteTempRoot(tempConfigDir);
        }
    }

    private static string FindWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sockseek.Server", "Sockseek.Server.csproj"))
                && File.Exists(Path.Combine(directory.FullName, "Sockseek.Desktop", "Sockseek.Desktop.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Sockseek workspace root for desktop SignalR integration tests.");
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (!Directory.Exists(tempRoot))
            return;

        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
