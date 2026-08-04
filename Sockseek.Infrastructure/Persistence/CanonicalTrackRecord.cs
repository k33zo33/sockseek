using Sockseek.Domain.Accounts;
using Sockseek.Domain.Tracks;

namespace Sockseek.Infrastructure.Persistence;

public sealed record CanonicalTrackRecord(
    string Artist,
    string Title,
    int? DurationMs,
    string? Isrc,
    string? MusicBrainzRecordingId,
    IReadOnlyList<TrackSourceRecord> Sources,
    IReadOnlyList<LocalMediaFileRecord> LocalMediaFiles);

public sealed record TrackSourceRecord(
    ExternalProvider Provider,
    string ExternalId,
    string? ExternalUrl,
    string? RawMetadataJson);

public sealed record LocalMediaFileRecord(
    string Path,
    long Size,
    DateTimeOffset LastWriteUtc,
    int? DurationMs,
    string? Codec,
    int? Bitrate,
    int? SampleRate,
    int? BitDepth,
    LocalMediaAvailability Availability);
