namespace Sockseek.Desktop;

public sealed class DesktopProgramBootstrap(
    DesktopProgramRunner runner,
    Func<DesktopProgramOptions, IDesktopShellSession> sessionFactory,
    Func<string> currentDirectoryProvider,
    IDesktopShellHost shellHost)
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => runner.RunAsync(args, (_, ct) => RunCoreAsync(args, ct), cancellationToken);

    private async Task<int> RunCoreAsync(string[] args, CancellationToken cancellationToken)
    {
        var options = DesktopProgramOptions.Parse(args, currentDirectoryProvider());
        await using var session = sessionFactory(options);

        var started = await session.StartAsync(cancellationToken);
        if (!started)
            return 2;

        if (options.ExitAfterStartup)
            return 0;

        return await shellHost.RunAsync(session, cancellationToken);
    }
}
