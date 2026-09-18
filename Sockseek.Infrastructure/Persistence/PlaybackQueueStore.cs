using Microsoft.EntityFrameworkCore;
using Sockseek.Application.Common;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class PlaybackQueueStore(SockseekDbContext dbContext, IClock clock)
{
    public async Task SaveAsync(PlaybackQueueSaveRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.Name))
            throw new ArgumentException("Queue name is required.", nameof(record));
        if (record.CurrentIndex < -1)
            throw new ArgumentOutOfRangeException(nameof(record), "Current index must be -1 or greater.");

        var orderedItems = record.Items
            .OrderBy(item => item.Position)
            .ToList();
        ValidateItemPositions(orderedItems);
        ValidateCurrentIndex(record.CurrentIndex, orderedItems.Count);

        var entity = await dbContext.PlaybackQueues
            .SingleOrDefaultAsync(queue => queue.Id == record.Id, cancellationToken);

        if (entity == null)
        {
            entity = new PlaybackQueueEntity { Id = record.Id };
            dbContext.PlaybackQueues.Add(entity);
        }
        else
        {
            await dbContext.PlaybackQueueItems
                .Where(item => item.QueueId == record.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }

        entity.Name = record.Name.Trim();
        entity.CurrentIndex = record.CurrentIndex;
        entity.RepeatMode = (int)record.RepeatMode;
        entity.ShuffleSeed = record.ShuffleSeed;
        entity.UpdatedAtUtc = clock.UtcNow;

        foreach (var item in orderedItems)
        {
            dbContext.PlaybackQueueItems.Add(new PlaybackQueueItemEntity
            {
                Id = item.Id,
                QueueId = record.Id,
                Position = item.Position,
                CanonicalTrackId = item.CanonicalTrackId,
                LocalMediaFileId = item.LocalMediaFileId,
                DownloadWorkflowId = item.DownloadWorkflowId,
                State = (int)item.State,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlaybackQueueRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PlaybackQueues
            .AsNoTracking()
            .Include(queue => queue.Items)
            .SingleOrDefaultAsync(queue => queue.Id == id, cancellationToken);

        return entity == null ? null : ToRecord(entity);
    }

    private static PlaybackQueueRecord ToRecord(PlaybackQueueEntity entity)
        => new(
            entity.Id,
            entity.Name,
            entity.CurrentIndex,
            (PlaybackQueueRepeatMode)entity.RepeatMode,
            entity.ShuffleSeed,
            entity.UpdatedAtUtc,
            entity.Items
                .OrderBy(item => item.Position)
                .Select(item => new PlaybackQueueItemRecord(
                    item.Id,
                    item.Position,
                    item.CanonicalTrackId,
                    item.LocalMediaFileId,
                    item.DownloadWorkflowId,
                    (PlaybackQueueItemState)item.State))
                .ToList());

    private static void ValidateItemPositions(IReadOnlyList<PlaybackQueueItemRecord> items)
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (items[index].Position != index)
                throw new ArgumentException("Queue item positions must be zero-based and contiguous.", nameof(items));
        }
    }

    private static void ValidateCurrentIndex(int currentIndex, int itemCount)
    {
        if (itemCount == 0 && currentIndex != -1)
            throw new ArgumentOutOfRangeException(nameof(currentIndex), "Empty queues must use current index -1.");
        if (itemCount > 0 && currentIndex >= itemCount)
            throw new ArgumentOutOfRangeException(nameof(currentIndex), "Current index must point to an existing queue item.");
    }
}
