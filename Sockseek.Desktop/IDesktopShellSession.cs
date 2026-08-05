namespace Sockseek.Desktop;

public interface IDesktopShellSession : IAsyncDisposable
{
    bool CanStartDaemon { get; }
    Task<bool> StartAsync(CancellationToken cancellationToken = default);
}
