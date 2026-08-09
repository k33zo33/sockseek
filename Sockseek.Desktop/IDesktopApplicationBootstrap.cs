namespace Sockseek.Desktop;

public interface IDesktopApplicationBootstrap
{
    Task<int> RunAsync(DesktopProgramBootstrap bootstrap, string[] args, CancellationToken cancellationToken = default);
}
