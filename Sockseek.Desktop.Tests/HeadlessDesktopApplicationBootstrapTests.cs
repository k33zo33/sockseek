using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class HeadlessDesktopApplicationBootstrapTests
{
    [TestMethod]
    public void RunAsync_WhenBootstrapIsNull_ThrowsArgumentNullException()
    {
        var applicationBootstrap = new HeadlessDesktopApplicationBootstrap(new FakeSingleInstanceGate(acquireLease: true));

        var exception = Assert.ThrowsException<ArgumentNullException>(() => applicationBootstrap.RunAsync(null!, [], CancellationToken.None));

        Assert.AreEqual("bootstrap", exception.ParamName);
    }

    [TestMethod]
    public void RunAsync_WhenArgsAreNull_ThrowsArgumentNullException()
    {
        var applicationBootstrap = new HeadlessDesktopApplicationBootstrap(new FakeSingleInstanceGate(acquireLease: true));
        var bootstrap = new DesktopProgramBootstrap(
            options => new FakeShellSession(canStartDaemon: true, startResult: true, options),
            () => "/workspace",
            new FakeShellHost());

        var exception = Assert.ThrowsException<ArgumentNullException>(() => applicationBootstrap.RunAsync(bootstrap, null!, CancellationToken.None));

        Assert.AreEqual("args", exception.ParamName);
    }

    [TestMethod]
    public async Task RunAsync_FirstInstance_RunsProgramBootstrapCore()
    {
        var gate = new FakeSingleInstanceGate(acquireLease: true);
        var shellHost = new FakeShellHost(exitCode: 7);
        FakeShellSession? createdSession = null;
        var bootstrap = new DesktopProgramBootstrap(
            options => createdSession = new FakeShellSession(canStartDaemon: true, startResult: true, options),
            () => "/workspace",
            shellHost);
        var applicationBootstrap = new HeadlessDesktopApplicationBootstrap(gate);

        var exitCode = await applicationBootstrap.RunAsync(bootstrap, []);

        Assert.AreEqual(7, exitCode);
        Assert.IsNotNull(createdSession);
        Assert.IsTrue(createdSession.StartCalled);
        Assert.AreEqual(1, shellHost.RunCallCount);
        Assert.AreEqual(1, gate.TryAcquireCallCount);
        Assert.IsTrue(gate.LastLease?.Disposed ?? false);
    }

    [TestMethod]
    public async Task RunAsync_SecondInstance_ReturnsNonZeroWithoutRunningProgramBootstrapCore()
    {
        var gate = new FakeSingleInstanceGate(acquireLease: false);
        var shellHost = new FakeShellHost();
        var bootstrap = new DesktopProgramBootstrap(
            options => new FakeShellSession(canStartDaemon: true, startResult: true, options),
            () => "/workspace",
            shellHost);
        var applicationBootstrap = new HeadlessDesktopApplicationBootstrap(gate);

        var exitCode = await applicationBootstrap.RunAsync(bootstrap, []);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(0, shellHost.RunCallCount);
        Assert.AreEqual(1, gate.TryAcquireCallCount);
    }

    [TestMethod]
    public void Constructor_WhenSingleInstanceGateIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsException<ArgumentNullException>(() => new HeadlessDesktopApplicationBootstrap(null!));

        Assert.AreEqual("singleInstanceGate", exception.ParamName);
    }

    private sealed class FakeSingleInstanceGate(bool acquireLease) : IDesktopSingleInstanceGate
    {
        public int TryAcquireCallCount { get; private set; }

        public FakeSingleInstanceLease? LastLease { get; private set; }

        public ValueTask<IDesktopSingleInstanceLease?> TryAcquireAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryAcquireCallCount++;

            if (!acquireLease)
                return ValueTask.FromResult<IDesktopSingleInstanceLease?>(null);

            LastLease = new FakeSingleInstanceLease();
            return ValueTask.FromResult<IDesktopSingleInstanceLease?>(LastLease);
        }
    }

    private sealed class FakeSingleInstanceLease : IDesktopSingleInstanceLease
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeShellSession(bool canStartDaemon, bool startResult, DesktopProgramOptions options) : IDesktopShellSession
    {
        public DesktopProgramOptions Options { get; } = options;

        public ShellNavigationViewModel Shell { get; } = new();

        public bool StartCalled { get; private set; }

        public bool CanStartDaemon => canStartDaemon;

        public Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalled = true;
            return Task.FromResult(startResult);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeShellHost(int exitCode = 0) : IDesktopShellHost
    {
        public int RunCallCount { get; private set; }

        public Task<int> RunAsync(IDesktopShellSession session, CancellationToken cancellationToken = default)
        {
            RunCallCount++;
            return Task.FromResult(exitCode);
        }
    }
}
