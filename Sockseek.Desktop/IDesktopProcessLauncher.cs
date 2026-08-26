namespace Sockseek.Desktop;

public interface IDesktopProcessLauncher
{
    Task<IDesktopProcessSession> LaunchAsync(DesktopDaemonLaunchRequest request, CancellationToken cancellationToken = default);
}

public interface IDesktopProcessSession : IAsyncDisposable
{
    IAsyncEnumerable<string> ReadOutputLinesAsync(CancellationToken cancellationToken = default);
}
