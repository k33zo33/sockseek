namespace Sockseek.Desktop;

public static class DesktopComposition
{
    public const string SingleInstanceMutexName = "Sockseek.Desktop.SingleInstance";

    public static IDesktopProgramFlow CreateProgramFlow(
        IDesktopShellWindowLifetime windowLifetime,
        Func<string>? currentDirectoryProvider = null,
        Func<DesktopProgramOptions, IDesktopShellSession>? sessionFactory = null,
        Func<IDesktopShellWindowLifetime, IDesktopShellHost>? shellHostFactory = null)
    {
        ArgumentNullException.ThrowIfNull(windowLifetime);

        var shellHost = (shellHostFactory ?? CreateShellHost)(windowLifetime);
        return new DesktopProgramBootstrap(
            sessionFactory ?? CreateShellSession,
            currentDirectoryProvider ?? Directory.GetCurrentDirectory,
            shellHost);
    }

    private static IDesktopShellHost CreateShellHost(IDesktopShellWindowLifetime windowLifetime)
        => new DesktopShellWindowHost(windowLifetime);

    public static IDesktopApplicationBootstrap CreateHeadlessApplicationBootstrap(IDesktopSingleInstanceGate? singleInstanceGate = null)
        => new HeadlessDesktopApplicationBootstrap(singleInstanceGate ?? new MutexDesktopSingleInstanceGate(SingleInstanceMutexName));

    private static IDesktopShellSession CreateShellSession(DesktopProgramOptions options)
        => new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(new SystemDesktopProcessLauncher()),
            themePreferenceStore: new DesktopFileThemePreferenceStore(DesktopSettingsPaths.GetThemePreferenceFilePath()),
            workspaceRoot: options.WorkspaceRoot);
}
