namespace Sockseek.Desktop;

public sealed class HeadlessDesktopApplicationBootstrap(IDesktopSingleInstanceGate singleInstanceGate) : IDesktopApplicationBootstrap
{
    private readonly IDesktopSingleInstanceGate singleInstanceGate = singleInstanceGate ?? throw new ArgumentNullException(nameof(singleInstanceGate));

    public Task<int> RunAsync(IDesktopProgramFlow programFlow, string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(programFlow);
        ArgumentNullException.ThrowIfNull(args);

        var runner = new DesktopProgramRunner(singleInstanceGate);
        return runner.RunAsync(args, (_, ct) => programFlow.RunAsync(args, ct), cancellationToken);
    }
}
