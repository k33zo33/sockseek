namespace Sockseek.Desktop;

public interface IDesktopShellWindowLifetime
{
    Task<int> RunAsync(DesktopShellWindowViewModel windowViewModel, CancellationToken cancellationToken = default);
}
