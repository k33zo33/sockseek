namespace Sockseek.Application.Playback;

public interface IPlaybackSourceResolver
{
    Task<PlaybackSourceResolution> ResolveCanonicalTrackAsync(
        Guid canonicalTrackId,
        CancellationToken cancellationToken = default);

    Task<PlaybackSourceResolution> ResolvePlaylistItemAsync(
        Guid playlistItemId,
        CancellationToken cancellationToken = default);
}
