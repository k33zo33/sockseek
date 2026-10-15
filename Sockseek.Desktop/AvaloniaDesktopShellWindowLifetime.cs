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

        if (RequiresDedicatedStaThread(OperatingSystem.IsWindows(), Thread.CurrentThread.GetApartmentState()))
            return RunOnDedicatedStaThreadAsync(windowViewModel, cancellationToken);

        return Task.FromResult(RunCore(windowViewModel, cancellationToken));
    }

    internal static bool RequiresDedicatedStaThread(bool isWindows, ApartmentState apartmentState)
        => isWindows && apartmentState != ApartmentState.STA;

    private static Task<int> RunOnDedicatedStaThreadAsync(
        DesktopShellWindowViewModel windowViewModel,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("A dedicated STA UI thread is only available on Windows.");

        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var uiThread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(RunCore(windowViewModel, cancellationToken));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        })
        {
            IsBackground = false,
            Name = "Sockseek Avalonia UI",
        };
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        return completion.Task;
    }

    private static int RunCore(
        DesktopShellWindowViewModel windowViewModel,
        CancellationToken cancellationToken)
    {
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

            return exitCode;
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
