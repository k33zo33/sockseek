namespace Sockseek.Desktop;

internal static class Program
{
    private static Task<int> Main(string[] args)
    {
        var programFlow = DesktopComposition.CreateProgramFlow(new HeadlessDesktopShellWindowLifetime());
        var applicationBootstrap = DesktopComposition.CreateApplicationBootstrap();

        return applicationBootstrap.RunAsync(programFlow, args);
    }
}
