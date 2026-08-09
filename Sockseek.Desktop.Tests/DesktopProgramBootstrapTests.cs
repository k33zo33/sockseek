using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopProgramBootstrapTests
{
    [TestMethod]
    public void RunAsync_WhenArgsAreNull_ThrowsArgumentNullException()
    {
        var bootstrap = new DesktopProgramBootstrap(
            options => new FakeShellSession(canStartDaemon: true, startResult: true, options),
            () => "/workspace",
            new FakeShellHost());

        var exception = Assert.ThrowsException<ArgumentNullException>(() => bootstrap.RunAsync(null!, CancellationToken.None));

        Assert.AreEqual("args", exception.ParamName);
    }

    [TestMethod]
    public async Task RunAsync_ExitAfterStartup_StartsShellSessionAndReturnsZero()
    {
        var shellHost = new FakeShellHost();
        FakeShellSession? createdSession = null;
        var bootstrap = new DesktopProgramBootstrap(
            options => createdSession = new FakeShellSession(canStartDaemon: true, startResult: true, options),
            () => "/workspace",
            shellHost);

        var exitCode = await bootstrap.RunAsync(["--exit-after-startup"]);

        Assert.AreEqual(0, exitCode);
        Assert.IsNotNull(createdSession);
        Assert.IsTrue(createdSession.StartCalled);
        Assert.AreEqual("/workspace", createdSession.Options.WorkspaceRoot);
        Assert.IsTrue(createdSession.Options.ExitAfterStartup);
        Assert.IsTrue(createdSession.Disposed);
        Assert.AreEqual(0, shellHost.RunCallCount);
    }

    [TestMethod]
    public async Task RunAsync_WhenSessionIsAlreadyConnectedAndDoesNotNeedLaunch_ReturnsZero()
    {
        var shellHost = new FakeShellHost();
        FakeShellSession? createdSession = null;
        var bootstrap = new DesktopProgramBootstrap(
            options => createdSession = new FakeShellSession(canStartDaemon: false, startResult: true, options),
            () => "/workspace",
            shellHost);

        var exitCode = await bootstrap.RunAsync(["--exit-after-startup"]);

        Assert.AreEqual(0, exitCode);
        Assert.IsNotNull(createdSession);
        Assert.IsTrue(createdSession.StartCalled);
        Assert.AreEqual(0, shellHost.RunCallCount);
    }

    [TestMethod]
    public async Task RunAsync_WhenSessionCannotStartDaemon_ReturnsTwoForExitAfterStartup()
    {
        var shellHost = new FakeShellHost();
        var bootstrap = new DesktopProgramBootstrap(
            options => new FakeShellSession(canStartDaemon: false, startResult: false, options),
            () => "/workspace",
            shellHost);

        var exitCode = await bootstrap.RunAsync(["--exit-after-startup"]);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(0, shellHost.RunCallCount);
    }

    [TestMethod]
    public async Task RunAsync_WhenDaemonStartFails_ReturnsTwoForExitAfterStartup()
    {
        var shellHost = new FakeShellHost();
        var bootstrap = new DesktopProgramBootstrap(
            options => new FakeShellSession(canStartDaemon: true, startResult: false, options),
            () => "/workspace",
            shellHost);

        var exitCode = await bootstrap.RunAsync(["--exit-after-startup", "--workspace-root", "/custom"]);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(0, shellHost.RunCallCount);
    }

    [TestMethod]
    public async Task RunAsync_WhenDaemonStartFailsInInteractiveMode_StillHostsShell()
    {
        var shellHost = new FakeShellHost(exitCode: 7);
        FakeShellSession? createdSession = null;
        var bootstrap = new DesktopProgramBootstrap(
            options => createdSession = new FakeShellSession(canStartDaemon: true, startResult: false, options),
            () => "/workspace",
            shellHost);

        var exitCode = await bootstrap.RunAsync(["--workspace-root", "/custom"]);

        Assert.AreEqual(7, exitCode);
        Assert.IsNotNull(createdSession);
        Assert.IsTrue(createdSession.StartCalled);
        Assert.AreSame(createdSession, shellHost.ReceivedSession);
        Assert.AreEqual(1, shellHost.RunCallCount);
        Assert.IsTrue(createdSession.Disposed);
    }

    [TestMethod]
    public async Task RunAsync_WhenStartupSucceedsAndAppKeepsRunning_DelegatesToShellHost()
    {
        var shellHost = new FakeShellHost(exitCode: 7);
        FakeShellSession? createdSession = null;
        var bootstrap = new DesktopProgramBootstrap(
            options => createdSession = new FakeShellSession(canStartDaemon: true, startResult: true, options),
            () => "/workspace",
            shellHost);

        var exitCode = await bootstrap.RunAsync([]);

        Assert.AreEqual(7, exitCode);
        Assert.IsNotNull(createdSession);
        Assert.AreSame(createdSession, shellHost.ReceivedSession);
        Assert.AreEqual(1, shellHost.RunCallCount);
        Assert.IsTrue(createdSession.Disposed);
    }

    [TestMethod]
    public void ParseOptions_UsesCurrentDirectoryAndRecognizesFlags()
    {
        var options = DesktopProgramOptions.Parse(["--exit-after-startup", "--workspace-root", "/custom"], "/workspace");

        Assert.AreEqual("/custom", options.WorkspaceRoot);
        Assert.IsTrue(options.ExitAfterStartup);
    }

    private sealed class FakeShellSession(bool canStartDaemon, bool startResult, DesktopProgramOptions options) : IDesktopShellSession
    {
        public DesktopProgramOptions Options { get; } = options;

        public ShellNavigationViewModel Shell { get; } = new();

        public bool StartCalled { get; private set; }

        public bool Disposed { get; private set; }

        public bool CanStartDaemon => canStartDaemon;

        public Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalled = true;
            return Task.FromResult(startResult);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeShellHost(int exitCode = 0) : IDesktopShellHost
    {
        public int RunCallCount { get; private set; }

        public IDesktopShellSession? ReceivedSession { get; private set; }

        public Task<int> RunAsync(IDesktopShellSession session, CancellationToken cancellationToken = default)
        {
            RunCallCount++;
            ReceivedSession = session;
            return Task.FromResult(exitCode);
        }
    }
}
