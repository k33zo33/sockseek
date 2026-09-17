using Microsoft.EntityFrameworkCore;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class LocalLibraryQueryStore(SockseekDbContext dbContext)
{
    public async Task<LocalLibrarySearchResult> SearchAsync(
        LocalLibrarySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        int offset = request.Offset < 0
            ? throw new ArgumentOutOfRangeException(nameof(request), "Offset must be zero or greater.")
            : request.Offset;
        int limit = request.Limit is < 1 or > 500
            ? throw new ArgumentOutOfRangeException(nameof(request), "Limit must be between 1 and 500.")
            : request.Limit;

        var query = dbContext.CanonicalTracks
            .AsNoTracking()
            .Where(track => track.LocalMediaFiles.Any(file =>
                request.IncludeMissing || file.Availability == (int)LocalMediaAvailability.Available));

        string normalizedSearch = NormalizeForSearch(request.SearchText);
        if (normalizedSearch.Length > 0)
        {
            string pattern = "%" + EscapeLikePattern(normalizedSearch) + "%";
            query = query.Where(track =>
                EF.Functions.Like(track.NormalizedArtist, pattern, "\\")
                || EF.Functions.Like(track.NormalizedTitle, pattern, "\\")
                || track.LocalMediaFiles.Any(file => EF.Functions.Like(file.Path, pattern, "\\")));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        var tracks = await query
            .OrderBy(track => track.NormalizedArtist)
            .ThenBy(track => track.NormalizedTitle)
            .ThenBy(track => track.DurationMs)
            .Skip(offset)
            .Take(limit)
            .Include(track => track.LocalMediaFiles)
            .ToListAsync(cancellationToken);

        return new LocalLibrarySearchResult(totalCount, tracks.Select(ToRecord).ToList());
    }

    private static LocalLibraryTrackRecord ToRecord(CanonicalTrackEntity track)
    {
        var availableFiles = track.LocalMediaFiles
            .Where(file => file.Availability == (int)LocalMediaAvailability.Available)
            .OrderByDescending(file => file.Bitrate ?? 0)
            .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var missingFiles = track.LocalMediaFiles
            .Where(file => file.Availability == (int)LocalMediaAvailability.Missing)
            .ToList();
        var best = availableFiles.FirstOrDefault();

        return new LocalLibraryTrackRecord(
            track.Id,
            track.Artist,
            track.Title,
            track.DurationMs,
            track.Isrc,
            track.MusicBrainzRecordingId,
            availableFiles.Count,
            missingFiles.Count,
            best?.Path,
            best?.Codec,
            best?.Bitrate,
            best?.SampleRate,
            best?.BitDepth);
    }

    private static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value.Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();

        return string.Join(' ', new string(chars)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string EscapeLikePattern(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
