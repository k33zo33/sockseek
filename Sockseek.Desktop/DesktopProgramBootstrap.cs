namespace Sockseek.Desktop;

public sealed class DesktopProgramBootstrap(
    Func<DesktopProgramOptions, IDesktopShellSession> sessionFactory,
    Func<string> currentDirectoryProvider,
    IDesktopShellHost shellHost) : IDesktopProgramFlow
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        => RunCoreAsync(args, cancellationToken);

    private async Task<int> RunCoreAsync(string[] args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = DesktopProgramOptions.Parse(args, currentDirectoryProvider());
        await using var session = sessionFactory(options);

        var started = await session.StartAsync(cancellationToken);
        if (options.ExitAfterStartup)
            return started ? 0 : 2;

        return await shellHost.RunAsync(session, cancellationToken);
    }
}
