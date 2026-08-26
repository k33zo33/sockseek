using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Sockseek.Desktop;

public sealed class AvaloniaDesktopShellWindowLifetime : IDesktopShellWindowLifetime
{
    public Task<int> RunAsync(DesktopShellWindowViewModel windowViewModel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(windowViewModel);
        cancellationToken.ThrowIfCancellationRequested();

        CancellationTokenRegistration cancellationRegistration = default;
        try
        {
            var exitCode = BuildApp()
                .StartWithClassicDesktopLifetime(
                    [],
                    lifetime =>
                    {
                        lifetime.ShutdownMode = ShutdownMode.OnMainWindowClose;
                        lifetime.MainWindow = new DesktopShellMainWindow
                        {
                            DataContext = windowViewModel,
                        };

                        if (cancellationToken.CanBeCanceled)
                        {
                            cancellationRegistration = cancellationToken.Register(() =>
                            {
                                Dispatcher.UIThread.Post(() => lifetime.Shutdown());
                            });
                        }
                    });

            return Task.FromResult(exitCode);
        }
        finally
        {
            cancellationRegistration.Dispose();
        }
    }

    internal static AppBuilder BuildApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
