using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopShellWindowHostTests
{
    [TestMethod]
    public void Constructor_WhenLifetimeIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsException<ArgumentNullException>(() => new DesktopShellWindowHost(null!));

        Assert.AreEqual("windowLifetime", exception.ParamName);
    }

    [TestMethod]
    public async Task RunAsync_CreatesWindowViewModelAndReturnsLifetimeExitCode()
    {
        await using var session = new FakeShellSession();
        var lifetime = new CapturingWindowLifetime { ExitCode = 5 };
        var host = new DesktopShellWindowHost(lifetime);

        var exitCode = await host.RunAsync(session);

        Assert.AreEqual(5, exitCode);
        Assert.IsNotNull(lifetime.ReceivedWindow);
        Assert.AreSame(session, lifetime.ReceivedWindow.Session);
        Assert.AreEqual(BackendConnectionState.Starting, lifetime.ReceivedWindow.BackendState);
    }

    [TestMethod]
    public async Task RunAsync_DisposesWindowViewModelAfterLifetimeCompletes()
    {
        await using var session = new FakeShellSession();
        var lifetime = new CapturingWindowLifetime
        {
            OnRunAsync = (_, _) => Task.FromResult(0)
        };
        var host = new DesktopShellWindowHost(lifetime);

        await host.RunAsync(session);

        Assert.IsNotNull(lifetime.ReceivedWindow);

        var changedProperties = new List<string?>();
        lifetime.ReceivedWindow.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);
        session.Shell.NavigateTo(ShellSection.Settings);

        Assert.AreEqual(0, changedProperties.Count);
    }

    [TestMethod]
    public async Task RunAsync_ForwardsCancellationTokenToLifetime()
    {
        await using var session = new FakeShellSession();
        var cancellationSource = new CancellationTokenSource();
        var lifetime = new CapturingWindowLifetime();
        var host = new DesktopShellWindowHost(lifetime);

        await host.RunAsync(session, cancellationSource.Token);

        Assert.AreEqual(cancellationSource.Token, lifetime.ReceivedCancellationToken);
    }

    [TestMethod]
    public async Task RunAsync_WhenLifetimeThrows_DisposesWindowViewModel()
    {
        await using var session = new FakeShellSession();
        var lifetime = new CapturingWindowLifetime
        {
            OnRunAsync = (_, _) => throw new InvalidOperationException("boom")
        };
        var host = new DesktopShellWindowHost(lifetime);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => host.RunAsync(session));

        Assert.IsNotNull(lifetime.ReceivedWindow);

        var changedProperties = new List<string?>();
        lifetime.ReceivedWindow.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);
        session.Shell.NavigateTo(ShellSection.Library);

        Assert.AreEqual(0, changedProperties.Count);
    }

    private sealed class FakeShellSession : IDesktopShellSession
    {
        public ShellNavigationViewModel Shell { get; } = new();

        public bool CanStartDaemon => false;

        public Task<bool> StartAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CapturingWindowLifetime : IDesktopShellWindowLifetime
    {
        public int ExitCode { get; init; }

        public Func<DesktopShellWindowViewModel, CancellationToken, Task<int>>? OnRunAsync { get; init; }

        public DesktopShellWindowViewModel? ReceivedWindow { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<int> RunAsync(DesktopShellWindowViewModel windowViewModel, CancellationToken cancellationToken = default)
        {
            ReceivedWindow = windowViewModel;
            ReceivedCancellationToken = cancellationToken;
            return OnRunAsync is not null
                ? OnRunAsync(windowViewModel, cancellationToken)
                : Task.FromResult(ExitCode);
        }
    }
}
