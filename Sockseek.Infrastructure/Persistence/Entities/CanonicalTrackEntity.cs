namespace Sockseek.Infrastructure.Persistence.Entities;

using Sockseek.Infrastructure.Persistence.Abstractions;

public sealed class CanonicalTrackEntity : IHasConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int? DurationMs { get; set; }
    public string? Isrc { get; set; }
    public string? MusicBrainzRecordingId { get; set; }
    public string NormalizedArtist { get; set; } = string.Empty;
    public string NormalizedTitle { get; set; } = string.Empty;

    public List<TrackSourceEntity> Sources { get; set; } = [];
    public List<LocalMediaFileEntity> LocalMediaFiles { get; set; } = [];
    public List<PlaylistItemEntity> PlaylistItems { get; set; } = [];
    public List<ResolutionAttemptEntity> ResolutionAttempts { get; set; } = [];
}
