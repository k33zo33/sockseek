using Sockseek.Domain.Accounts;

namespace Sockseek.Domain.Tracks;

public sealed record TrackIdentityQuery(
    string Artist,
    string Title,
    int? DurationMs = null,
    string? Isrc = null,
    string? MusicBrainzRecordingId = null,
    ExternalProvider? SourceProvider = null,
    string? SourceExternalId = null,
    string? Album = null);
