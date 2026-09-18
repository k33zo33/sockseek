namespace Sockseek.Infrastructure.Persistence;

public enum PlaybackQueueItemState
{
    PendingResolution = 0,
    LocalFile = 1,
    ProgressiveDownload = 2,
    Failed = 3,
}
