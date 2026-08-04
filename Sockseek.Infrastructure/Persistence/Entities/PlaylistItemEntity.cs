namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class PlaylistItemEntity
{
    public Guid Id { get; set; }
    public Guid PlaylistId { get; set; }
    public int Position { get; set; }
    public string ProviderItemId { get; set; } = string.Empty;
    public Guid? CanonicalTrackId { get; set; }
    public int Status { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTimeOffset? RemovedAtUtc { get; set; }

    public PlaylistEntity Playlist { get; set; } = null!;
    public CanonicalTrackEntity? CanonicalTrack { get; set; }
    public List<ResolutionAttemptEntity> ResolutionAttempts { get; set; } = [];
    public List<DownloadWorkflowEntity> DownloadWorkflows { get; set; } = [];
}
