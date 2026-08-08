namespace Sockseek.Desktop;

public sealed class DesktopShellWindowHost(
    Func<DesktopShellWindowViewModel, CancellationToken, Task<int>> runWindowAsync) : IDesktopShellHost
{
    private readonly Func<DesktopShellWindowViewModel, CancellationToken, Task<int>> runWindowAsync = runWindowAsync ?? throw new ArgumentNullException(nameof(runWindowAsync));

    public async Task<int> RunAsync(IDesktopShellSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        using var windowViewModel = new DesktopShellWindowViewModel(session);
        return await runWindowAsync(windowViewModel, cancellationToken);
    }
}
