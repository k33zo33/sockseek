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
            IClassicDesktopStyleApplicationLifetime? desktopLifetime = null;
            var exitCode = BuildApp()
                .AfterSetup(_ =>
                {
                    var lifetime = desktopLifetime
                        ?? throw new InvalidOperationException("Avalonia desktop lifetime was not configured before setup completed.");

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
                })
                .StartWithClassicDesktopLifetime(
                    [],
                    lifetime =>
                    {
                        desktopLifetime = lifetime;
                        lifetime.ShutdownMode = ShutdownMode.OnMainWindowClose;
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
