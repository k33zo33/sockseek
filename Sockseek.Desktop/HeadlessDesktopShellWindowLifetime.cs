namespace Sockseek.Desktop;

public sealed class HeadlessDesktopShellWindowLifetime : IDesktopShellWindowLifetime
{
    public async Task<int> RunAsync(DesktopShellWindowViewModel windowViewModel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(windowViewModel);

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
    }
}
