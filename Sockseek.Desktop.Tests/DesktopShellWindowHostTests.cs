using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopShellWindowHostTests
{
    [TestMethod]
    public async Task RunAsync_CreatesWindowViewModelAndReturnsDelegateExitCode()
    {
        await using var session = new FakeShellSession();
        DesktopShellWindowViewModel? receivedWindow = null;
        var host = new DesktopShellWindowHost((windowViewModel, _) =>
        {
            receivedWindow = windowViewModel;
            return Task.FromResult(5);
        });

        var exitCode = await host.RunAsync(session);

        Assert.AreEqual(5, exitCode);
        Assert.IsNotNull(receivedWindow);
        Assert.AreSame(session, receivedWindow.Session);
        Assert.AreEqual(BackendConnectionState.Starting, receivedWindow.BackendState);
    }

    [TestMethod]
    public async Task RunAsync_DisposesWindowViewModelAfterDelegateCompletes()
    {
        await using var session = new FakeShellSession();
        DesktopShellWindowViewModel? receivedWindow = null;
        var host = new DesktopShellWindowHost(async (windowViewModel, _) =>
        {
            receivedWindow = windowViewModel;
            await Task.Yield();
            return 0;
        });

        await host.RunAsync(session);

        Assert.IsNotNull(receivedWindow);

        var changedProperties = new List<string?>();
        receivedWindow.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);
        session.Shell.NavigateTo(ShellSection.Settings);

        Assert.AreEqual(0, changedProperties.Count);
    }

    [TestMethod]
    public async Task RunAsync_ForwardsCancellationTokenToDelegate()
    {
        await using var session = new FakeShellSession();
        CancellationToken receivedToken = default;
        var cancellationSource = new CancellationTokenSource();
        var host = new DesktopShellWindowHost((_, cancellationToken) =>
        {
            receivedToken = cancellationToken;
            return Task.FromResult(0);
        });

        await host.RunAsync(session, cancellationSource.Token);

        Assert.AreEqual(cancellationSource.Token, receivedToken);
    }

    [TestMethod]
    public async Task RunAsync_WhenDelegateThrows_DisposesWindowViewModel()
    {
        await using var session = new FakeShellSession();
        DesktopShellWindowViewModel? receivedWindow = null;
        var host = new DesktopShellWindowHost((windowViewModel, _) =>
        {
            receivedWindow = windowViewModel;
            throw new InvalidOperationException("boom");
        });

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => host.RunAsync(session));

        Assert.IsNotNull(receivedWindow);

        var changedProperties = new List<string?>();
        receivedWindow.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);
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
}
