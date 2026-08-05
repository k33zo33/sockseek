namespace Sockseek.Desktop;

internal static class Program
{
    private const string SingleInstanceMutexName = "Sockseek.Desktop.SingleInstance";

    private static Task<int> Main(string[] args)
    {
        var runner = new DesktopProgramRunner(new MutexDesktopSingleInstanceGate(SingleInstanceMutexName));
        var bootstrap = new DesktopProgramBootstrap(
            runner,
            options => new DesktopShellSession(
                supervisor: new DesktopDaemonSupervisor(new SystemDesktopProcessLauncher()),
                workspaceRoot: options.WorkspaceRoot),
            Directory.GetCurrentDirectory);

        return bootstrap.RunAsync(args);
    }
}
