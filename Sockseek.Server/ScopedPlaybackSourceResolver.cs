using Microsoft.Extensions.DependencyInjection;
using Sockseek.Application.Playback;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Server;

public sealed class ScopedPlaybackSourceResolver(IServiceScopeFactory scopeFactory) : IPlaybackSourceResolver
{
    public async Task<PlaybackSourceResolution> ResolveCanonicalTrackAsync(
        Guid canonicalTrackId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<LocalPlaybackSourceResolver>();
        return await resolver.ResolveCanonicalTrackAsync(canonicalTrackId, cancellationToken);
    }

    public async Task<PlaybackSourceResolution> ResolvePlaylistItemAsync(
        Guid playlistItemId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<LocalPlaybackSourceResolver>();
        return await resolver.ResolvePlaylistItemAsync(playlistItemId, cancellationToken);
    }
}
