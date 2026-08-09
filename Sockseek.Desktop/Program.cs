namespace Sockseek.Desktop;

internal static class Program
{
    private const string SingleInstanceMutexName = "Sockseek.Desktop.SingleInstance";

    private static Task<int> Main(string[] args)
    {
        var shellHost = new DesktopShellWindowHost(new HeadlessDesktopShellWindowLifetime());
        var bootstrap = new DesktopProgramBootstrap(
            options => new DesktopShellSession(
                supervisor: new DesktopDaemonSupervisor(new SystemDesktopProcessLauncher()),
                themePreferenceStore: new DesktopFileThemePreferenceStore(DesktopSettingsPaths.GetThemePreferenceFilePath()),
                workspaceRoot: options.WorkspaceRoot),
            Directory.GetCurrentDirectory,
            shellHost);
        var applicationBootstrap = new HeadlessDesktopApplicationBootstrap(new MutexDesktopSingleInstanceGate(SingleInstanceMutexName));

        return applicationBootstrap.RunAsync(bootstrap, args);
    }
}
