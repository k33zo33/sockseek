namespace Sockseek.Infrastructure.Persistence;

public sealed record LocalLibraryTrackRecord(
    Guid TrackId,
    string Artist,
    string Title,
    int? DurationMs,
    string? Isrc,
    string? MusicBrainzRecordingId,
    int AvailableFileCount,
    int MissingFileCount,
    string? BestAvailablePath,
    string? Codec,
    int? Bitrate,
    int? SampleRate,
    int? BitDepth);
