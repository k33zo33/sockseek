namespace Sockseek.Desktop;

public sealed class DesktopProgramRunner(IDesktopSingleInstanceGate singleInstanceGate)
{
    public async Task<int> RunAsync(
        string[] args,
        Func<string[], CancellationToken, Task<int>> startAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(startAsync);

        await using var lease = await singleInstanceGate.TryAcquireAsync(cancellationToken);
        if (lease is null)
            return 1;

        return await startAsync(args, cancellationToken);
    }
}
