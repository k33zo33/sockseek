namespace Sockseek.Desktop;

public sealed class HeadlessDesktopShellWindowLifetime : IDesktopShellWindowLifetime
{
    public Task<int> RunAsync(DesktopShellWindowViewModel windowViewModel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(windowViewModel);

        return RunCoreAsync(cancellationToken);
    }

    private static async Task<int> RunCoreAsync(CancellationToken cancellationToken)
    {
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
