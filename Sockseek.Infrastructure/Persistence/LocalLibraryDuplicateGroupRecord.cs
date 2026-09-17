namespace Sockseek.Infrastructure.Persistence;

public sealed record LocalLibraryDuplicateGroupRecord(
    Guid TrackId,
    string Artist,
    string Title,
    int? DurationMs,
    int FileCount,
    IReadOnlyList<LocalLibraryDuplicateFileRecord> Files);
