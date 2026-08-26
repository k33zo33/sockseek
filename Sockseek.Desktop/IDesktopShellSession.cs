namespace Sockseek.Desktop;

public interface IDesktopShellSession : IAsyncDisposable
{
    ShellNavigationViewModel Shell { get; }
    bool CanStartDaemon { get; }
    Task<bool> StartAsync(CancellationToken cancellationToken = default);
}
