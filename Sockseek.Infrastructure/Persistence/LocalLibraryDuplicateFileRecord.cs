using Sockseek.Domain.Tracks;

namespace Sockseek.Infrastructure.Persistence;

public sealed record LocalLibraryDuplicateFileRecord(
    Guid LocalMediaFileId,
    string Path,
    long Size,
    int? DurationMs,
    string? Codec,
    int? Bitrate,
    int? SampleRate,
    int? BitDepth,
    LocalMediaAvailability Availability);
