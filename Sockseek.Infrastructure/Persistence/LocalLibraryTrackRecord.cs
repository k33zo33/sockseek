namespace Sockseek.Infrastructure.Persistence;

public sealed record LocalLibraryTrackRecord(
    Guid TrackId,
    string Artist,
    string Title,
    string? AlbumTitle,
    int? DurationMs,
    string? Isrc,
    string? MusicBrainzRecordingId,
    int AvailableFileCount,
    int MissingFileCount,
    Guid? BestAvailableFileId,
    string? BestAvailablePath,
    string? Codec,
    int? Bitrate,
    int? SampleRate,
    int? BitDepth);
