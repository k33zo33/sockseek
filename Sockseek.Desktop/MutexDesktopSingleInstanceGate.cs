using System.Threading;

namespace Sockseek.Desktop;

public sealed class MutexDesktopSingleInstanceGate(string mutexName) : IDesktopSingleInstanceGate
{
    private readonly string mutexName = string.IsNullOrWhiteSpace(mutexName)
        ? throw new ArgumentException("Mutex name is required.", nameof(mutexName))
        : mutexName;

    public ValueTask<IDesktopSingleInstanceLease?> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return ValueTask.FromResult<IDesktopSingleInstanceLease?>(null);
        }

        return ValueTask.FromResult<IDesktopSingleInstanceLease?>(new MutexDesktopSingleInstanceLease(mutex));
    }

    private sealed class MutexDesktopSingleInstanceLease(Mutex mutex) : IDesktopSingleInstanceLease
    {
        private readonly Mutex mutex = mutex;
        private bool disposed;

        public ValueTask DisposeAsync()
        {
            if (disposed)
                return ValueTask.CompletedTask;

            disposed = true;
            mutex.ReleaseMutex();
            mutex.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
