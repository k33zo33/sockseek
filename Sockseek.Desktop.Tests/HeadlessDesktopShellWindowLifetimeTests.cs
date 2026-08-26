using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class HeadlessDesktopShellWindowLifetimeTests
{
    [TestMethod]
    public async Task RunAsync_WhenWindowViewModelIsNull_ThrowsArgumentNullException()
    {
        var lifetime = new HeadlessDesktopShellWindowLifetime();

        var exception = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => lifetime.RunAsync(null!, CancellationToken.None));

        Assert.AreEqual("windowViewModel", exception.ParamName);
    }

    [TestMethod]
    public async Task RunAsync_WhenCancellationIsAlreadyRequested_ReturnsZero()
    {
        await using var session = new FakeShellSession();
        using var windowViewModel = new DesktopShellWindowViewModel(session);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var lifetime = new HeadlessDesktopShellWindowLifetime();

        var exitCode = await lifetime.RunAsync(windowViewModel, cancellationSource.Token);

        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_WaitsUntilCancellationAndThenReturnsZero()
    {
        await using var session = new FakeShellSession();
        using var windowViewModel = new DesktopShellWindowViewModel(session);
        using var cancellationSource = new CancellationTokenSource();
        var lifetime = new HeadlessDesktopShellWindowLifetime();

        var runTask = lifetime.RunAsync(windowViewModel, cancellationSource.Token);
        Assert.IsFalse(runTask.IsCompleted);

        cancellationSource.Cancel();
        var exitCode = await runTask;

        Assert.AreEqual(0, exitCode);
    }

    private sealed class FakeShellSession : IDesktopShellSession
    {
        public ShellNavigationViewModel Shell { get; } = new();

        public bool CanStartDaemon => false;

        public Task<bool> StartAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
