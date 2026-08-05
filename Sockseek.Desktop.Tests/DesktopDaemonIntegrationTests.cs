using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public class DesktopDaemonIntegrationTests
{
    [TestMethod]
    public async Task TryLaunchAsync_DevelopmentDaemonLaunchRequest_StartsRealServerAndSupportsAuthenticatedSystemApi()
    {
        var workspaceRoot = FindWorkspaceRoot();
        var request = DesktopDevelopmentDaemonLaunchRequestFactory.Create(workspaceRoot);
        await using var supervisor = new DesktopDaemonSupervisor(new SystemDesktopProcessLauncher());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        var launched = await supervisor.TryLaunchAsync(request, cts.Token);

        Assert.IsTrue(launched);
        Assert.AreEqual(BackendConnectionState.Connected, supervisor.State);
        Assert.IsNotNull(supervisor.CurrentHandshake);

        var apiClient = DesktopBackendClientFactory.CreateApiClient(supervisor.CurrentHandshake);
        var systemInfo = await apiClient.GetSystemInfoAsync(cts.Token);
        var health = await apiClient.GetSystemHealthAsync(cts.Token);

        Assert.AreEqual("Sockseek", systemInfo.Name);
        Assert.IsTrue(systemInfo.Capabilities.VersionedApi);
        Assert.AreEqual("ok", health.Status);
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

        throw new DirectoryNotFoundException("Could not locate the Sockseek workspace root for desktop integration tests.");
    }
}
