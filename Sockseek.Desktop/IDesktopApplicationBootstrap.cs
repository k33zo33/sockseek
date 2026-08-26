namespace Sockseek.Desktop;

public interface IDesktopApplicationBootstrap
{
    Task<int> RunAsync(IDesktopProgramFlow programFlow, string[] args, CancellationToken cancellationToken = default);
}
