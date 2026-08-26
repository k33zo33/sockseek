using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopBackendRecoveryCoordinatorIntegrationTests
{
    [TestMethod]
    public async Task RecoveryCoordinator_RelaunchesRealDaemon_AndReconnectsWithFreshHandshake()
    {
        var workspaceRoot = FindWorkspaceRoot();
        var request = DesktopDevelopmentDaemonLaunchRequestFactory.Create(workspaceRoot);
        await using var supervisor = new DesktopDaemonSupervisor(new SystemDesktopProcessLauncher());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));

        var launched = await supervisor.TryLaunchAsync(request, cts.Token);
        Assert.IsTrue(launched);
        Assert.IsNotNull(supervisor.CurrentHandshake);

        await using var coordinator = new DesktopBackendRecoveryCoordinator(supervisor);
        var states = new List<DesktopBackendEventsConnectionState>();
        coordinator.EventsStateChanged += (_, state) => states.Add(state);

        await WaitForAsync(
            () => coordinator.EventsState == DesktopBackendEventsConnectionState.Connected,
            TimeSpan.FromSeconds(30),
            cts.Token);

        var firstHandshake = supervisor.CurrentHandshake;
        var relaunchResult = await supervisor.TryLaunchAsync(request, cts.Token);
        Assert.IsTrue(relaunchResult);
        Assert.IsNotNull(supervisor.CurrentHandshake);

        await WaitForAsync(
            () => coordinator.EventsState == DesktopBackendEventsConnectionState.Connected,
            TimeSpan.FromSeconds(30),
            cts.Token);

        var secondHandshake = supervisor.CurrentHandshake;
        Assert.IsNotNull(firstHandshake);
        Assert.IsNotNull(secondHandshake);
        Assert.AreNotEqual(firstHandshake.SessionToken, secondHandshake.SessionToken);
        Assert.IsTrue(states.Contains(DesktopBackendEventsConnectionState.Disconnected));
        Assert.IsTrue(states.Count(state => state == DesktopBackendEventsConnectionState.Connected) >= 2);

        var apiClient = DesktopBackendClientFactory.CreateApiClient(secondHandshake);
        var health = await apiClient.GetSystemHealthAsync(cts.Token);
        Assert.AreEqual("ok", health.Status);
    }

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        while (!predicate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow - startedAt > timeout)
                throw new TimeoutException($"Condition was not satisfied within {timeout}.");

            await Task.Delay(100, cancellationToken);
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

        throw new DirectoryNotFoundException("Could not locate the Sockseek workspace root for desktop recovery integration tests.");
    }
}
