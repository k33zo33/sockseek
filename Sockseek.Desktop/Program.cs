namespace Sockseek.Desktop;

internal static class Program
{
    private const string SingleInstanceMutexName = "Sockseek.Desktop.SingleInstance";

    private static Task<int> Main(string[] args)
    {
        var runner = new DesktopProgramRunner(new MutexDesktopSingleInstanceGate(SingleInstanceMutexName));
        var shellHost = new DesktopShellWindowHost(new HeadlessDesktopShellWindowLifetime());
        var bootstrap = new DesktopProgramBootstrap(
            runner,
            options => new DesktopShellSession(
                supervisor: new DesktopDaemonSupervisor(new SystemDesktopProcessLauncher()),
                themePreferenceStore: new DesktopFileThemePreferenceStore(DesktopSettingsPaths.GetThemePreferenceFilePath()),
                workspaceRoot: options.WorkspaceRoot),
            Directory.GetCurrentDirectory,
            shellHost);

        return bootstrap.RunAsync(args);
    }
}
