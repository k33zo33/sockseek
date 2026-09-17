namespace Sockseek.Infrastructure.Persistence;

public sealed record LocalLibrarySearchResult(
    int TotalCount,
    IReadOnlyList<LocalLibraryTrackRecord> Items);
