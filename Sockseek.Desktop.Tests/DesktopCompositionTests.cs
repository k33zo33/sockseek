using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopCompositionTests
{
    [TestMethod]
    public async Task CreateProgramFlow_UsesInjectedSessionFactoryAndCurrentDirectoryProvider()
    {
        var currentDirectoryCalls = 0;
        var shellHost = new FakeWindowLifetime();
        FakeShellSession? createdSession = null;
        var programFlow = DesktopComposition.CreateProgramFlow(
            shellHost,
            currentDirectoryProvider: () =>
            {
                currentDirectoryCalls++;
                return "/workspace";
            },
            sessionFactory: options => createdSession = new FakeShellSession(options, startResult: true));

        var exitCode = await programFlow.RunAsync(["--workspace-root", "/custom"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(1, currentDirectoryCalls);
        Assert.IsNotNull(createdSession);
        Assert.AreEqual("/custom", createdSession.Options.WorkspaceRoot);
        Assert.IsTrue(createdSession.StartCalled);
        Assert.AreEqual(1, shellHost.RunCallCount);
    }

    [TestMethod]
    public async Task CreateProgramFlow_WhenWorkspaceRootIsImplicit_UsesCurrentDirectoryProvider()
    {
        var shellHost = new FakeWindowLifetime();
        FakeShellSession? createdSession = null;
        var programFlow = DesktopComposition.CreateProgramFlow(
            shellHost,
            currentDirectoryProvider: () => "/workspace",
            sessionFactory: options => createdSession = new FakeShellSession(options, startResult: true));

        await programFlow.RunAsync([]);

        Assert.IsNotNull(createdSession);
        Assert.AreEqual("/workspace", createdSession.Options.WorkspaceRoot);
    }

    [TestMethod]
    public void CreateProgramFlow_WhenWindowLifetimeIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsException<ArgumentNullException>(() => DesktopComposition.CreateProgramFlow(null!));

        Assert.AreEqual("windowLifetime", exception.ParamName);
    }

    [TestMethod]
    public async Task CreateHeadlessApplicationBootstrap_UsesInjectedSingleInstanceGate()
    {
        var gate = new FakeSingleInstanceGate(acquireLease: true);
        var bootstrap = DesktopComposition.CreateHeadlessApplicationBootstrap(gate);
        var programFlow = new FakeProgramFlow(exitCode: 3);

        var exitCode = await bootstrap.RunAsync(programFlow, []);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(1, gate.TryAcquireCallCount);
        Assert.AreEqual(1, programFlow.RunCallCount);
    }

    [TestMethod]
    public async Task CreateHeadlessApplicationBootstrap_WhenLeaseIsUnavailable_DoesNotRunProgramFlow()
    {
        var gate = new FakeSingleInstanceGate(acquireLease: false);
        var bootstrap = DesktopComposition.CreateHeadlessApplicationBootstrap(gate);
        var programFlow = new FakeProgramFlow(exitCode: 3);

        var exitCode = await bootstrap.RunAsync(programFlow, []);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(1, gate.TryAcquireCallCount);
        Assert.AreEqual(0, programFlow.RunCallCount);
    }

    private sealed class FakeWindowLifetime : IDesktopShellWindowLifetime
    {
        public int RunCallCount { get; private set; }

        public Task<int> RunAsync(DesktopShellWindowViewModel windowViewModel, CancellationToken cancellationToken = default)
        {
            RunCallCount++;
            return Task.FromResult(0);
        }
    }

    private sealed class FakeShellSession(DesktopProgramOptions options, bool startResult) : IDesktopShellSession
    {
        public DesktopProgramOptions Options { get; } = options;

        public ShellNavigationViewModel Shell { get; } = new();

        public bool CanStartDaemon => true;

        public bool StartCalled { get; private set; }

        public Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalled = true;
            return Task.FromResult(startResult);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeProgramFlow(int exitCode) : IDesktopProgramFlow
    {
        public int RunCallCount { get; private set; }

        public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        {
            RunCallCount++;
            return Task.FromResult(exitCode);
        }
    }

    private sealed class FakeSingleInstanceGate(bool acquireLease) : IDesktopSingleInstanceGate
    {
        public int TryAcquireCallCount { get; private set; }

        public ValueTask<IDesktopSingleInstanceLease?> TryAcquireAsync(CancellationToken cancellationToken = default)
        {
            TryAcquireCallCount++;
            return ValueTask.FromResult<IDesktopSingleInstanceLease?>(acquireLease ? new FakeLease() : null);
        }
    }

    private sealed class FakeLease : IDesktopSingleInstanceLease
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
