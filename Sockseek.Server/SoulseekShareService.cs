using Microsoft.EntityFrameworkCore;
using Sockseek.Application.Common;
using Sockseek.Core.Services;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.SoulseekSharing;

namespace Sockseek.Server;

public sealed class SoulseekShareService : ISoulseekShareSource, IDisposable
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly SoulseekShareSnapshotBuilder snapshotBuilder;
    private readonly SemaphoreSlim rebuildLock = new(1, 1);
    private readonly Lock stateLock = new();
    private SoulseekShareBuildResult current = new(SoulseekShareSnapshot.Empty, []);
    private long revision;
    private bool disposed;

    public SoulseekShareService(
        IServiceScopeFactory scopeFactory,
        SoulseekShareSnapshotBuilder snapshotBuilder)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.snapshotBuilder = snapshotBuilder ?? throw new ArgumentNullException(nameof(snapshotBuilder));
    }

    public event EventHandler? SnapshotChanged;

    public SoulseekShareSourceSnapshot GetSnapshot()
    {
        lock (stateLock)
        {
            return new SoulseekShareSourceSnapshot(
                revision,
                current.Snapshot.DirectoryCount,
                current.Snapshot.Files
                    .Select(file => new SoulseekShareSourceFile(
                        file.RemoteFilename,
                        file.Size,
                        Path.GetExtension(file.RemoteFilename)))
                    .ToArray());
        }
    }

    public SoulseekShareBuildResult GetBuildResult()
    {
        lock (stateLock)
            return current;
    }

    public async Task<SoulseekShareSourceSnapshot> RebuildAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await rebuildLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var configuredRoots = await new SoulseekShareRootStore(dbContext, clock)
                .ListAsync(cancellationToken);
            var rebuilt = await Task.Run(
                () => snapshotBuilder.Build(configuredRoots, cancellationToken),
                cancellationToken);

            SoulseekShareSourceSnapshot snapshot;
            lock (stateLock)
            {
                current = rebuilt;
                revision++;
                snapshot = GetSnapshotLocked();
            }

            SnapshotChanged?.Invoke(this, EventArgs.Empty);
            return snapshot;
        }
        finally
        {
            rebuildLock.Release();
        }
    }

    public Task<Stream> OpenReadAsync(
        string remoteFilename,
        long offset,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        SoulseekSharedFile? sharedFile;
        lock (stateLock)
        {
            if (!current.Snapshot.TryGetFile(remoteFilename, out sharedFile) || sharedFile is null)
                throw new FileNotFoundException("The requested shared file is unavailable.");
        }

        return Task.FromResult<Stream>(SoulseekSharedFileAccess.OpenRead(sharedFile, offset));
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        rebuildLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private SoulseekShareSourceSnapshot GetSnapshotLocked()
        => new(
            revision,
            current.Snapshot.DirectoryCount,
            current.Snapshot.Files
                .Select(file => new SoulseekShareSourceFile(
                    file.RemoteFilename,
                    file.Size,
                    Path.GetExtension(file.RemoteFilename)))
                .ToArray());
}
