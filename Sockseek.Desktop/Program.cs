namespace Sockseek.Desktop;

internal static class Program
{
    private const string SingleInstanceMutexName = "Sockseek.Desktop.SingleInstance";

    private static async Task<int> Main(string[] args)
    {
        var runner = new DesktopProgramRunner(new MutexDesktopSingleInstanceGate(SingleInstanceMutexName));
        return await runner.RunAsync(args, static (_, _) => Task.FromResult(0));
    }
}
