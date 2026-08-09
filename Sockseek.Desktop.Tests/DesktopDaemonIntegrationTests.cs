using System.Net;
using System.Net.Http.Headers;
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

    [TestMethod]
    public async Task TryLaunchAsync_HandshakeToken_ProtectsVersionedApiAndLeavesHealthOpen()
    {
        var workspaceRoot = FindWorkspaceRoot();
        var request = DesktopDevelopmentDaemonLaunchRequestFactory.Create(workspaceRoot);
        await using var supervisor = new DesktopDaemonSupervisor(new SystemDesktopProcessLauncher());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        var launched = await supervisor.TryLaunchAsync(request, cts.Token);
        Assert.IsTrue(launched);
        Assert.IsNotNull(supervisor.CurrentHandshake);

        using var anonymousClient = new HttpClient { BaseAddress = new Uri(supervisor.CurrentHandshake.BaseUrl) };
        using var protectedResponse = await anonymousClient.GetAsync("/api/v1/system/info", cts.Token);
        using var healthResponse = await anonymousClient.GetAsync("/api/v1/system/health", cts.Token);

        Assert.AreEqual(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        Assert.IsTrue(protectedResponse.Headers.WwwAuthenticate.Any(header => string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(HttpStatusCode.OK, healthResponse.StatusCode);

        var apiClient = DesktopBackendClientFactory.CreateApiClient(supervisor.CurrentHandshake);
        var systemInfo = await apiClient.GetSystemInfoAsync(cts.Token);
        Assert.AreEqual("Sockseek", systemInfo.Name);
    }

    [TestMethod]
    public async Task TryLaunchAsync_RelaunchRotatesSessionToken_AndRejectsOldToken()
    {
        var workspaceRoot = FindWorkspaceRoot();
        var request = DesktopDevelopmentDaemonLaunchRequestFactory.Create(workspaceRoot);
        await using var supervisor = new DesktopDaemonSupervisor(new SystemDesktopProcessLauncher());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));

        var firstLaunch = await supervisor.TryLaunchAsync(request, cts.Token);
        Assert.IsTrue(firstLaunch);
        Assert.IsNotNull(supervisor.CurrentHandshake);

        var firstHandshake = supervisor.CurrentHandshake;
        var secondLaunch = await supervisor.TryLaunchAsync(request, cts.Token);
        Assert.IsTrue(secondLaunch);
        Assert.IsNotNull(supervisor.CurrentHandshake);

        var secondHandshake = supervisor.CurrentHandshake;
        Assert.AreNotEqual(firstHandshake.SessionToken, secondHandshake.SessionToken);

        using var staleTokenClient = new HttpClient { BaseAddress = new Uri(secondHandshake.BaseUrl) };
        staleTokenClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstHandshake.SessionToken);
        using var staleTokenResponse = await staleTokenClient.GetAsync("/api/v1/system/info", cts.Token);
        Assert.AreEqual(HttpStatusCode.Unauthorized, staleTokenResponse.StatusCode);
        Assert.IsTrue(staleTokenResponse.Headers.WwwAuthenticate.Any(header => string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)));

        using var freshTokenClient = new HttpClient { BaseAddress = new Uri(secondHandshake.BaseUrl) };
        freshTokenClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondHandshake.SessionToken);
        using var freshTokenResponse = await freshTokenClient.GetAsync("/api/v1/system/info", cts.Token);
        Assert.AreEqual(HttpStatusCode.OK, freshTokenResponse.StatusCode);

        var freshClient = DesktopBackendClientFactory.CreateApiClient(secondHandshake);
        var systemInfo = await freshClient.GetSystemInfoAsync(cts.Token);
        Assert.AreEqual("Sockseek", systemInfo.Name);
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
