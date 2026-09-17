using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sockseek.Domain.Playlists;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class PlaylistLocalMatchResolver(
    SockseekDbContext dbContext,
    TrackIdentityService identityService)
{
    public async Task<PlaylistLocalMatchResult> ResolveAsync(Guid playlistId, CancellationToken cancellationToken = default)
    {
        var items = await dbContext.PlaylistItems
            .Where(item => item.PlaylistId == playlistId
                && item.RemovedAtUtc == null
                && (item.Status == (int)PlaylistItemStatus.Imported
                    || item.Status == (int)PlaylistItemStatus.Unresolved
                    || item.Status == (int)PlaylistItemStatus.ReviewRequired))
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return new PlaylistLocalMatchResult(0, 0, 0);

        var candidates = await dbContext.CanonicalTracks
            .Include(track => track.Sources)
            .Where(track => track.LocalMediaFiles.Any(file => file.Availability == (int)LocalMediaAvailability.Available))
            .ToListAsync(cancellationToken);

        int matchedItems = 0;
        int reviewItems = 0;
        int unresolvedItems = 0;

        foreach (var item in items)
        {
            var snapshot = JsonSerializer.Deserialize<ExternalPlaylistItemSnapshot>(item.SnapshotJson)
                ?? throw new InvalidOperationException("Playlist item snapshot could not be deserialized.");

            var query = new TrackIdentityQuery(snapshot.Artist, snapshot.Title, snapshot.DurationMs, Album: snapshot.Album);
            var bestMatch = FindBestMatch(candidates, query);

            if (bestMatch is { Result.Disposition: TrackMatchDisposition.AutoMatch })
            {
                item.CanonicalTrackId = bestMatch.Track.Id;
                item.Status = (int)PlaylistItemStatus.AvailableLocal;
                matchedItems++;
                continue;
            }

            if (bestMatch is { Result.Disposition: TrackMatchDisposition.ReviewRequired })
            {
                item.CanonicalTrackId = bestMatch.Track.Id;
                item.Status = (int)PlaylistItemStatus.ReviewRequired;
                reviewItems++;
                continue;
            }

            item.Status = (int)PlaylistItemStatus.Unresolved;
            unresolvedItems++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new PlaylistLocalMatchResult(matchedItems, reviewItems, unresolvedItems);
    }

    private LocalMatchCandidate? FindBestMatch(IReadOnlyList<CanonicalTrackEntity> candidates, TrackIdentityQuery query)
    {
        LocalMatchCandidate? best = null;
        foreach (var candidate in candidates)
        {
            var result = identityService.Match(ToDomain(candidate), query);
            if (best == null || result.Score > best.Result.Score)
                best = new LocalMatchCandidate(candidate, result);
        }

        return best?.Result.Disposition == TrackMatchDisposition.NoMatch ? null : best;
    }

    private static CanonicalTrack ToDomain(CanonicalTrackEntity entity)
    {
        var track = new CanonicalTrack(
            entity.Artist,
            entity.Title,
            entity.DurationMs,
            entity.Isrc,
            entity.MusicBrainzRecordingId);

        foreach (var source in entity.Sources)
        {
            track.AddSource(
                (Domain.Accounts.ExternalProvider)source.Provider,
                source.ExternalId,
                source.ExternalUrl,
                source.RawMetadataJson);
        }

        return track;
    }

    private sealed record LocalMatchCandidate(CanonicalTrackEntity Track, TrackMatchResult Result);
}
