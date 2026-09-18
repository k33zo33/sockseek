using Sockseek.Application.Playback;

namespace Sockseek.Player;

public sealed class PlaybackCoordinator(IPlaybackSourceResolver sourceResolver)
{
    public Task<PlaybackSourceResolution> ResolveCanonicalTrackAsync(
        Guid canonicalTrackId,
        CancellationToken cancellationToken = default)
        => sourceResolver.ResolveCanonicalTrackAsync(canonicalTrackId, cancellationToken);

    public Task<PlaybackSourceResolution> ResolvePlaylistItemAsync(
        Guid playlistItemId,
        CancellationToken cancellationToken = default)
        => sourceResolver.ResolvePlaylistItemAsync(playlistItemId, cancellationToken);
}
