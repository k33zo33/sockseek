using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public class DesktopBackendEventsConnectionIntegrationTests
{
    [TestMethod]
    public async Task ReconnectManager_StartSubscribeStop_WorksAgainstRealDaemon()
    {
        var workspaceRoot = FindWorkspaceRoot();
        var request = DesktopDevelopmentDaemonLaunchRequestFactory.Create(workspaceRoot);
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
}
