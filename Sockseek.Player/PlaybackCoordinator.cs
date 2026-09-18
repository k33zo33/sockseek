using Sockseek.Application.Playback;

namespace Sockseek.Player;

public sealed class PlaybackCoordinator
{
    private readonly IPlaybackSourceResolver sourceResolver;
    private readonly IMediaEngine mediaEngine;

    private PlaybackSnapshot snapshot = PlaybackSnapshot.Stopped;

    public PlaybackCoordinator(IPlaybackSourceResolver sourceResolver)
        : this(sourceResolver, new UnavailableMediaEngine())
    {
    }

    public PlaybackCoordinator(IPlaybackSourceResolver sourceResolver, IMediaEngine mediaEngine)
    {
        this.sourceResolver = sourceResolver;
        this.mediaEngine = mediaEngine;
    }

    public PlaybackSnapshot Snapshot => snapshot;

    public Task<PlaybackSourceResolution> ResolveCanonicalTrackAsync(
        Guid canonicalTrackId,
        CancellationToken cancellationToken = default)
        => sourceResolver.ResolveCanonicalTrackAsync(canonicalTrackId, cancellationToken);

    public Task<PlaybackSourceResolution> ResolvePlaylistItemAsync(
        Guid playlistItemId,
        CancellationToken cancellationToken = default)
        => sourceResolver.ResolvePlaylistItemAsync(playlistItemId, cancellationToken);

    public async Task<PlaybackSnapshot> PlayCanonicalTrackAsync(
        Guid canonicalTrackId,
        CancellationToken cancellationToken = default)
    {
        snapshot = new PlaybackSnapshot(PlaybackState.ResolvingSource, canonicalTrackId, null, null, null, null);
        var source = await sourceResolver.ResolveCanonicalTrackAsync(canonicalTrackId, cancellationToken);
        return await PlayResolvedSourceAsync(source, cancellationToken);
    }

    public async Task<PlaybackSnapshot> PlayPlaylistItemAsync(
        Guid playlistItemId,
        CancellationToken cancellationToken = default)
    {
        snapshot = new PlaybackSnapshot(PlaybackState.ResolvingSource, null, playlistItemId, null, null, null);
        var source = await sourceResolver.ResolvePlaylistItemAsync(playlistItemId, cancellationToken);
        return await PlayResolvedSourceAsync(source, cancellationToken);
    }

    public async Task<PlaybackSnapshot> PauseAsync(CancellationToken cancellationToken = default)
    {
        if (snapshot.State != PlaybackState.Playing)
            return snapshot;

        await mediaEngine.PauseAsync(cancellationToken);
        snapshot = snapshot with { State = PlaybackState.Paused, ErrorMessage = null };
        return snapshot;
    }

    public async Task<PlaybackSnapshot> ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (snapshot.State != PlaybackState.Paused)
            return snapshot;

        await mediaEngine.PlayAsync(cancellationToken);
        snapshot = snapshot with { State = PlaybackState.Playing, ErrorMessage = null };
        return snapshot;
    }

    public async Task<PlaybackSnapshot> StopAsync(CancellationToken cancellationToken = default)
    {
        await mediaEngine.StopAsync(cancellationToken);
        snapshot = PlaybackSnapshot.Stopped;
        return snapshot;
    }

    private async Task<PlaybackSnapshot> PlayResolvedSourceAsync(
        PlaybackSourceResolution source,
        CancellationToken cancellationToken)
    {
        if (source.Kind != PlaybackSourceKind.LocalFile || string.IsNullOrWhiteSpace(source.Path))
        {
            snapshot = new PlaybackSnapshot(
                PlaybackState.Failed,
                source.CanonicalTrackId,
                source.PlaylistItemId,
                source.LocalMediaFileId,
                source.Path,
                source.Reason ?? "Playback source is not available.");
            return snapshot;
        }

        if (IsNonFileUri(source.Path))
        {
            snapshot = new PlaybackSnapshot(
                PlaybackState.Failed,
                source.CanonicalTrackId,
                source.PlaylistItemId,
                source.LocalMediaFileId,
                source.Path,
                "Playback source must be a local file path.");
            return snapshot;
        }

        snapshot = new PlaybackSnapshot(
            PlaybackState.Loading,
            source.CanonicalTrackId,
            source.PlaylistItemId,
            source.LocalMediaFileId,
            source.Path,
            null);

        try
        {
            await mediaEngine.LoadAsync(source.Path, cancellationToken);
            await mediaEngine.PlayAsync(cancellationToken);
            snapshot = snapshot with { State = PlaybackState.Playing };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            snapshot = snapshot with
            {
                State = PlaybackState.Failed,
                ErrorMessage = ex.Message,
            };
        }

        return snapshot;
    }

    private static bool IsNonFileUri(string path)
    {
        if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
            return false;

        return !uri.IsFile && uri.Scheme.Length > 1;
    }
}
