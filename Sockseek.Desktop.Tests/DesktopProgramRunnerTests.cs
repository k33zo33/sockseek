using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopProgramRunnerTests
{
    [TestMethod]
    public async Task RunAsync_FirstInstance_InvokesStartupCallback()
    {
        var gate = new FakeSingleInstanceGate(acquireLease: true);
        var runner = new DesktopProgramRunner(gate);
        var callbackInvoked = false;

        var exitCode = await runner.RunAsync(["--headless"], (_, _) =>
        {
            callbackInvoked = true;
            return Task.FromResult(7);
        });

        Assert.AreEqual(7, exitCode);
        Assert.IsTrue(callbackInvoked);
        Assert.AreEqual(1, gate.TryAcquireCallCount);
        Assert.IsTrue(gate.LastLease?.Disposed ?? false);
    }

    [TestMethod]
    public async Task RunAsync_SecondInstance_ReturnsNonZeroWithoutInvokingStartup()
    {
        var gate = new FakeSingleInstanceGate(acquireLease: false);
        var runner = new DesktopProgramRunner(gate);
        var callbackInvoked = false;

        var exitCode = await runner.RunAsync([], (_, _) =>
        {
            callbackInvoked = true;
            return Task.FromResult(0);
        });

        Assert.AreEqual(1, exitCode);
        Assert.IsFalse(callbackInvoked);
        Assert.AreEqual(1, gate.TryAcquireCallCount);
    }

    [TestMethod]
    public async Task MutexDesktopSingleInstanceGate_ReleasesLease_ForFutureAcquire()
    {
        var gate = new MutexDesktopSingleInstanceGate($"Sockseek.Desktop.Tests.{Guid.NewGuid():N}");

        var firstLease = await gate.TryAcquireAsync();
        Assert.IsNotNull(firstLease);

        var secondLeaseWhileHeld = await gate.TryAcquireAsync();
        Assert.IsNull(secondLeaseWhileHeld);

        await firstLease.DisposeAsync();

        var thirdLeaseAfterRelease = await gate.TryAcquireAsync();
        Assert.IsNotNull(thirdLeaseAfterRelease);
        await thirdLeaseAfterRelease.DisposeAsync();
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
}
