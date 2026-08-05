using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopProgramBootstrapTests
{
    [TestMethod]
    public async Task RunAsync_ExitAfterStartup_StartsShellSessionAndReturnsZero()
    {
        var runner = new DesktopProgramRunner(new AlwaysFirstInstanceGate());
        FakeShellSession? createdSession = null;
        var bootstrap = new DesktopProgramBootstrap(
            runner,
            options => createdSession = new FakeShellSession(canStartDaemon: true, startResult: true, options),
            () => "/workspace");

        var exitCode = await bootstrap.RunAsync(["--exit-after-startup"]);

        Assert.AreEqual(0, exitCode);
        Assert.IsNotNull(createdSession);
        Assert.IsTrue(createdSession.StartCalled);
        Assert.AreEqual("/workspace", createdSession.Options.WorkspaceRoot);
        Assert.IsTrue(createdSession.Options.ExitAfterStartup);
        Assert.IsTrue(createdSession.Disposed);
    }

    [TestMethod]
    public async Task RunAsync_WhenSessionCannotStartDaemon_ReturnsTwo()
    {
        var runner = new DesktopProgramRunner(new AlwaysFirstInstanceGate());
        var bootstrap = new DesktopProgramBootstrap(
            runner,
            options => new FakeShellSession(canStartDaemon: false, startResult: false, options),
            () => "/workspace");

        var exitCode = await bootstrap.RunAsync(["--exit-after-startup"]);

        Assert.AreEqual(2, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_WhenDaemonStartFails_ReturnsTwo()
    {
        var runner = new DesktopProgramRunner(new AlwaysFirstInstanceGate());
        var bootstrap = new DesktopProgramBootstrap(
            runner,
            options => new FakeShellSession(canStartDaemon: true, startResult: false, options),
            () => "/workspace");

        var exitCode = await bootstrap.RunAsync(["--exit-after-startup", "--workspace-root", "/custom"]);

        Assert.AreEqual(2, exitCode);
    }

    [TestMethod]
    public void ParseOptions_UsesCurrentDirectoryAndRecognizesFlags()
    {
        var options = DesktopProgramOptions.Parse(["--exit-after-startup", "--workspace-root", "/custom"], "/workspace");

        Assert.AreEqual("/custom", options.WorkspaceRoot);
        Assert.IsTrue(options.ExitAfterStartup);
    }

    private sealed class AlwaysFirstInstanceGate : IDesktopSingleInstanceGate
    {
        public ValueTask<IDesktopSingleInstanceLease?> TryAcquireAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IDesktopSingleInstanceLease?>(new Lease());

        private sealed class Lease : IDesktopSingleInstanceLease
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FakeShellSession(bool canStartDaemon, bool startResult, DesktopProgramOptions options) : IDesktopShellSession
    {
        public DesktopProgramOptions Options { get; } = options;

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
}
