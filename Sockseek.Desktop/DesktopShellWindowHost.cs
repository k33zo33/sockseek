namespace Sockseek.Desktop;

public sealed class DesktopShellWindowHost(IDesktopShellWindowLifetime windowLifetime) : IDesktopShellHost
{
    private readonly IDesktopShellWindowLifetime windowLifetime = windowLifetime ?? throw new ArgumentNullException(nameof(windowLifetime));

    public async Task<int> RunAsync(IDesktopShellSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        using var windowViewModel = new DesktopShellWindowViewModel(session);
        return await windowLifetime.RunAsync(windowViewModel, cancellationToken);
    }
}
