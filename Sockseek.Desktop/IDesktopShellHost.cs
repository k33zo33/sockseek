namespace Sockseek.Desktop;

public interface IDesktopShellHost
{
    Task<int> RunAsync(IDesktopShellSession session, CancellationToken cancellationToken = default);
}
