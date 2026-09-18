namespace Sockseek.Infrastructure.Persistence.Entities;

using Sockseek.Infrastructure.Persistence.Abstractions;

public sealed class PlaybackQueueItemEntity : IHasConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public Guid QueueId { get; set; }
    public int Position { get; set; }
    public Guid CanonicalTrackId { get; set; }
    public Guid? LocalMediaFileId { get; set; }
    public Guid? DownloadWorkflowId { get; set; }
    public int State { get; set; }

    public PlaybackQueueEntity? Queue { get; set; }
    public CanonicalTrackEntity? CanonicalTrack { get; set; }
    public LocalMediaFileEntity? LocalMediaFile { get; set; }
    public DownloadWorkflowEntity? DownloadWorkflow { get; set; }
}
