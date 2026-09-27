namespace Sockseek.Domain.Playlists;

public sealed record ExternalPlaylistItemSnapshot(
    string ProviderItemId,
    int Position,
    string Title,
    string Artist,
    string? Album,
    int? DurationMs,
    string? ExternalTrackId = null,
    string? Isrc = null,
    string? ExternalUrl = null,
    string? ArtworkUrl = null,
    string? MusicBrainzRecordingId = null,
    IReadOnlyList<string>? Artists = null,
    string? RawMetadataJson = null);
