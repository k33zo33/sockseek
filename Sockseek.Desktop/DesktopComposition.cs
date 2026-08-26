namespace Sockseek.Desktop;

public static class DesktopComposition
{
    public const string SingleInstanceMutexName = "Sockseek.Desktop.SingleInstance";

    public static IDesktopProgramFlow CreateProgramFlow(
        IDesktopShellHost shellHost,
        Func<string>? currentDirectoryProvider = null,
        Func<DesktopProgramOptions, IDesktopShellSession>? sessionFactory = null)
    {
        ArgumentNullException.ThrowIfNull(shellHost);

        return new DesktopProgramBootstrap(
            sessionFactory ?? CreateShellSession,
            currentDirectoryProvider ?? Directory.GetCurrentDirectory,
            shellHost);
    }

    public static IDesktopProgramFlow CreateProgramFlow(
        IDesktopShellWindowLifetime windowLifetime,
        Func<string>? currentDirectoryProvider = null,
        Func<DesktopProgramOptions, IDesktopShellSession>? sessionFactory = null,
        Func<IDesktopShellWindowLifetime, IDesktopShellHost>? shellHostFactory = null)
    {
        ArgumentNullException.ThrowIfNull(windowLifetime);

        var shellHost = (shellHostFactory ?? CreateShellHost)(windowLifetime);
        return CreateProgramFlow(shellHost, currentDirectoryProvider, sessionFactory);
    }

    public static IDesktopShellHost CreateShellHost(IDesktopShellWindowLifetime windowLifetime)
    {
        ArgumentNullException.ThrowIfNull(windowLifetime);

        return new DesktopShellWindowHost(windowLifetime);
    }

    public static IDesktopApplicationBootstrap CreateApplicationBootstrap(IDesktopSingleInstanceGate? singleInstanceGate = null)
        => new SingleInstanceDesktopApplicationBootstrap(singleInstanceGate ?? new MutexDesktopSingleInstanceGate(SingleInstanceMutexName));

    public static IDesktopApplicationBootstrap CreateHeadlessApplicationBootstrap(IDesktopSingleInstanceGate? singleInstanceGate = null)
        => CreateApplicationBootstrap(singleInstanceGate);

    private static IDesktopShellSession CreateShellSession(DesktopProgramOptions options)
        => new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(new SystemDesktopProcessLauncher()),
            themePreferenceStore: new DesktopFileThemePreferenceStore(DesktopSettingsPaths.GetThemePreferenceFilePath()),
            workspaceRoot: options.WorkspaceRoot);
}
