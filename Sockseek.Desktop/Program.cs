namespace Sockseek.Desktop;

internal static class Program
{
    [STAThread]
    private static Task<int> Main(string[] args)
    {
        var programFlow = DesktopComposition.CreateProgramFlow(UseHeadlessLifetime(args)
            ? new HeadlessDesktopShellWindowLifetime()
            : new AvaloniaDesktopShellWindowLifetime());
        var applicationBootstrap = DesktopComposition.CreateApplicationBootstrap();

        return applicationBootstrap.RunAsync(programFlow, args);
    }

    private static bool UseHeadlessLifetime(string[] args)
        => args.Any(argument => string.Equals(argument, "--headless", StringComparison.OrdinalIgnoreCase));
}
