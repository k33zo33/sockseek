using Sockseek.Domain.Tracks;

namespace Sockseek.Infrastructure.Persistence;

public sealed record LocalLibraryDuplicateFileRecord(
    string Path,
    long Size,
    int? DurationMs,
    string? Codec,
    int? Bitrate,
    int? SampleRate,
    int? BitDepth,
    LocalMediaAvailability Availability);
