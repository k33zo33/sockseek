namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class ResolutionAttemptEntity
{
    public Guid Id { get; set; }
    public Guid PlaylistItemId { get; set; }
    public Guid? CandidateTrackId { get; set; }
    public Guid? EngineJobId { get; set; }
    public int Method { get; set; }
    public double Score { get; set; }
    public int Decision { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public PlaylistItemEntity PlaylistItem { get; set; } = null!;
    public CanonicalTrackEntity? CandidateTrack { get; set; }
}
