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

        return ValueTask.FromResult<IDesktopSingleInstanceLease?>(
            MutexDesktopSingleInstanceLease.TryCreate(mutexName));
    }

    private sealed class MutexDesktopSingleInstanceLease : IDesktopSingleInstanceLease
    {
        private readonly string mutexName;
        private readonly ManualResetEventSlim acquisitionCompleted = new(initialState: false);
        private readonly ManualResetEventSlim releaseRequested = new(initialState: false);
        private readonly TaskCompletionSource ownerCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Thread ownerThread;
        private Exception? acquisitionException;
        private bool acquired;
        private int disposeStarted;

        private MutexDesktopSingleInstanceLease(string mutexName)
        {
            this.mutexName = mutexName;
            ownerThread = new Thread(OwnMutex)
            {
                IsBackground = true,
                Name = "Sockseek desktop single-instance gate",
            };
        }

        public static MutexDesktopSingleInstanceLease? TryCreate(string mutexName)
        {
            var lease = new MutexDesktopSingleInstanceLease(mutexName);
            lease.ownerThread.Start();
            lease.acquisitionCompleted.Wait();

            if (lease.acquisitionException is not null)
            {
                lease.DisposeSynchronizationPrimitives();
                throw new InvalidOperationException("Failed to acquire the Desktop single-instance mutex.", lease.acquisitionException);
            }

            if (lease.acquired)
                return lease;

            lease.ownerCompleted.Task.GetAwaiter().GetResult();
            lease.DisposeSynchronizationPrimitives();
            return null;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref disposeStarted, 1, 0) == 0)
                releaseRequested.Set();

            try
            {
                await ownerCompleted.Task.ConfigureAwait(false);
            }
            finally
            {
                if (Interlocked.CompareExchange(ref disposeStarted, 2, 1) == 1)
                    DisposeSynchronizationPrimitives();
            }
        }

        private void OwnMutex()
        {
            Exception? ownerException = null;
            var acquisitionReported = false;

            try
            {
                using var mutex = new Mutex(initiallyOwned: false, mutexName);
                try
                {
                    acquired = mutex.WaitOne(millisecondsTimeout: 0);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }

                acquisitionCompleted.Set();
                acquisitionReported = true;

                if (!acquired)
                    return;

                releaseRequested.Wait();
                mutex.ReleaseMutex();
            }
            catch (Exception exception)
            {
                if (acquisitionReported)
                    ownerException = exception;
                else
                    acquisitionException = exception;
            }
            finally
            {
                acquisitionCompleted.Set();
                if (ownerException is null)
                    ownerCompleted.TrySetResult();
                else
                    ownerCompleted.TrySetException(ownerException);
            }
        }

        private void DisposeSynchronizationPrimitives()
        {
            acquisitionCompleted.Dispose();
            releaseRequested.Dispose();
        }
    }
}
