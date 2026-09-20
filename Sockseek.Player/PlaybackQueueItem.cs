namespace Sockseek.Player;

public sealed record PlaybackQueueItem(
    Guid Id,
    Guid CanonicalTrackId,
    Guid? LocalMediaFileId = null,
    Guid? DownloadWorkflowId = null);
