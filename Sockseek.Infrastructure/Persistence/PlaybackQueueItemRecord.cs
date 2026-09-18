namespace Sockseek.Infrastructure.Persistence;

public sealed record PlaybackQueueItemRecord(
    Guid Id,
    int Position,
    Guid CanonicalTrackId,
    Guid? LocalMediaFileId,
    Guid? DownloadWorkflowId,
    PlaybackQueueItemState State);
