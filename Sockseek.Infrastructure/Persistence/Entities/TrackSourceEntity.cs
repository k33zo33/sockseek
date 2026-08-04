namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class TrackSourceEntity
{
    public Guid Id { get; set; }
    public Guid CanonicalTrackId { get; set; }
    public int Provider { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
    public string? RawMetadataJson { get; set; }

    public CanonicalTrackEntity CanonicalTrack { get; set; } = null!;
}
