using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sockseek.Infrastructure;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Player;

namespace Sockseek.Server;

public sealed class PlaybackQueuePersistenceService(
    IServiceScopeFactory scopeFactory,
    ServerDatabaseMigrationService databaseMigration,
    PlaybackCoordinator player)
{
    public static Guid DefaultQueueId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task RestoreDefaultQueueAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
        // Hosted services also run during build-time OpenAPI generation; avoid migrating
        // a user database just to restore optional queue state.
        if (!await PlaybackQueueTablesExistAsync(db, cancellationToken))
            return;

        var record = await new PlaybackQueueStore(db, new SystemClock())
            .GetAsync(DefaultQueueId, cancellationToken);
        if (record is null)
            return;

        player.SetQueue(
            record.Items
                .Select(item => new PlaybackQueueItem(
                    item.Id,
                    item.CanonicalTrackId,
                    item.LocalMediaFileId,
                    item.DownloadWorkflowId))
                .ToArray(),
            record.CurrentIndex,
            ToPlayerRepeatMode(record.RepeatMode),
            record.ShuffleEnabled,
            record.ShuffleSeed);
    }

    public async Task SaveDefaultQueueAsync(CancellationToken cancellationToken = default)
    {
        var queue = player.Queue;
        await databaseMigration.EnsureMigratedAsync(cancellationToken);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();

        await new PlaybackQueueStore(db, new SystemClock()).SaveAsync(new PlaybackQueueSaveRecord(
            DefaultQueueId,
            "Main queue",
            queue.CurrentIndex,
            ToPersistenceRepeatMode(queue.RepeatMode),
            queue.ShuffleEnabled,
            queue.ShuffleSeed,
            queue.Items
                .Select((item, index) => new PlaybackQueueItemRecord(
                    item.Id,
                    index,
                    item.CanonicalTrackId,
                    item.LocalMediaFileId,
                    item.DownloadWorkflowId,
                    item.LocalMediaFileId is null
                        ? PlaybackQueueItemState.PendingResolution
                        : PlaybackQueueItemState.LocalFile))
                .ToArray()),
            cancellationToken);
    }

    private static async Task<bool> PlaybackQueueTablesExistAsync(
        SockseekDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await db.Database
                .SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name IN ('PlaybackQueues', 'PlaybackQueueItems')")
                .SingleAsync(cancellationToken);
            return count == 2;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 14)
        {
            return false;
        }
    }

    private static PlaybackRepeatMode ToPlayerRepeatMode(PlaybackQueueRepeatMode repeatMode)
        => repeatMode switch
        {
            PlaybackQueueRepeatMode.One => PlaybackRepeatMode.One,
            PlaybackQueueRepeatMode.All => PlaybackRepeatMode.All,
            _ => PlaybackRepeatMode.None,
        };

    private static PlaybackQueueRepeatMode ToPersistenceRepeatMode(PlaybackRepeatMode repeatMode)
        => repeatMode switch
        {
            PlaybackRepeatMode.One => PlaybackQueueRepeatMode.One,
            PlaybackRepeatMode.All => PlaybackQueueRepeatMode.All,
            _ => PlaybackQueueRepeatMode.None,
        };
}
