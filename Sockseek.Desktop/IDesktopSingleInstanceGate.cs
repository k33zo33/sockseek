namespace Sockseek.Desktop;

public interface IDesktopSingleInstanceGate
{
    ValueTask<IDesktopSingleInstanceLease?> TryAcquireAsync(CancellationToken cancellationToken = default);
}

public interface IDesktopSingleInstanceLease : IAsyncDisposable
{
}
