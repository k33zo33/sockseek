namespace Sockseek.Desktop;

public interface IDesktopProgramFlow
{
    Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default);
}
