using Microsoft.EntityFrameworkCore;
using Sockseek.Application.Playback;
using Sockseek.Domain.Playlists;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class LocalPlaybackSourceResolver(SockseekDbContext dbContext) : IPlaybackSourceResolver
{
    public async Task<PlaybackSourceResolution> ResolveCanonicalTrackAsync(
        Guid canonicalTrackId,
        CancellationToken cancellationToken = default)
    {
        var file = await BestAvailableFileQuery()
            .Where(mediaFile => mediaFile.CanonicalTrackId == canonicalTrackId)
            .FirstOrDefaultAsync(cancellationToken);

        return file == null
            ? PlaybackSourceResolution.Unavailable(canonicalTrackId, null, "No available local media file was found for this track.")
            : PlaybackSourceResolution.LocalFile(canonicalTrackId, null, file.Id, file.Path);
    }

    public async Task<PlaybackSourceResolution> ResolvePlaylistItemAsync(
        Guid playlistItemId,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.PlaylistItems
            .AsNoTracking()
            .Where(candidate => candidate.Id == playlistItemId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.CanonicalTrackId,
                candidate.Status,
                candidate.RemovedAtUtc,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (item == null)
            return PlaybackSourceResolution.Unavailable(null, playlistItemId, "Playlist item was not found.");

        if (item.RemovedAtUtc != null)
            return PlaybackSourceResolution.Unavailable(item.CanonicalTrackId, playlistItemId, "Playlist item was removed from the source playlist.");

        if (item.CanonicalTrackId == null || item.Status != (int)PlaylistItemStatus.AvailableLocal)
            return PlaybackSourceResolution.PendingResolution(playlistItemId, "Playlist item has not resolved to an available local file.");

        var file = await BestAvailableFileQuery()
            .Where(mediaFile => mediaFile.CanonicalTrackId == item.CanonicalTrackId)
            .FirstOrDefaultAsync(cancellationToken);

        return file == null
            ? PlaybackSourceResolution.Unavailable(item.CanonicalTrackId, playlistItemId, "Matched track has no available local media file.")
            : PlaybackSourceResolution.LocalFile(item.CanonicalTrackId.Value, playlistItemId, file.Id, file.Path);
    }

    private IQueryable<LocalMediaFileEntity> BestAvailableFileQuery()
        => dbContext.LocalMediaFiles
            .AsNoTracking()
            .Where(mediaFile => mediaFile.Availability == (int)LocalMediaAvailability.Available)
            .OrderByDescending(mediaFile => mediaFile.Bitrate ?? 0)
            .ThenByDescending(mediaFile => mediaFile.SampleRate ?? 0)
            .ThenByDescending(mediaFile => mediaFile.BitDepth ?? 0)
            .ThenBy(mediaFile => mediaFile.Path);
}
