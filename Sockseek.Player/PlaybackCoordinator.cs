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
        snapshot = snapshot with
        {
            State = PlaybackState.ResolvingSource,
            CanonicalTrackId = canonicalTrackId,
            PlaylistItemId = null,
            LocalMediaFileId = null,
            Path = null,
            ErrorMessage = null,
            Position = TimeSpan.Zero,
        };
        var source = await sourceResolver.ResolveCanonicalTrackAsync(canonicalTrackId, cancellationToken);
        return await PlayResolvedSourceAsync(source, cancellationToken);
    }

    public async Task<PlaybackSnapshot> PlayPlaylistItemAsync(
        Guid playlistItemId,
        CancellationToken cancellationToken = default)
    {
        snapshot = snapshot with
        {
            State = PlaybackState.ResolvingSource,
            CanonicalTrackId = null,
            PlaylistItemId = playlistItemId,
            LocalMediaFileId = null,
            Path = null,
            ErrorMessage = null,
            Position = TimeSpan.Zero,
        };
        var source = await sourceResolver.ResolvePlaylistItemAsync(playlistItemId, cancellationToken);
        return await PlayResolvedSourceAsync(source, cancellationToken);
    }

    public async Task<PlaybackSnapshot> PauseAsync(CancellationToken cancellationToken = default)
    {
        if (snapshot.State != PlaybackState.Playing)
            return snapshot;

        return await RunEngineCommandAsync(
            engine => engine.PauseAsync(cancellationToken),
            () => snapshot with { State = PlaybackState.Paused, ErrorMessage = null });
    }

    public async Task<PlaybackSnapshot> ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (snapshot.State != PlaybackState.Paused)
            return snapshot;

        return await RunEngineCommandAsync(
            engine => engine.PlayAsync(cancellationToken),
            () => snapshot with { State = PlaybackState.Playing, ErrorMessage = null });
    }

    public async Task<PlaybackSnapshot> StopAsync(CancellationToken cancellationToken = default)
    {
        return await RunEngineCommandAsync(
            engine => engine.StopAsync(cancellationToken),
            () => PlaybackSnapshot.Stopped with
            {
                Volume = snapshot.Volume,
                IsMuted = snapshot.IsMuted,
            });
    }

    public async Task<PlaybackSnapshot> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        if (position < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(position), "Seek position cannot be negative.");
        if (snapshot.State is not (PlaybackState.Playing or PlaybackState.Paused))
            return snapshot;

        return await RunEngineCommandAsync(
            engine => engine.SeekAsync(position, cancellationToken),
            () => snapshot with { Position = position, ErrorMessage = null });
    }

    public async Task<PlaybackSnapshot> SetVolumeAsync(double volume, CancellationToken cancellationToken = default)
    {
        if (volume is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0 and 1.");

        return await RunEngineCommandAsync(
            engine => engine.SetVolumeAsync(volume, cancellationToken),
            () => snapshot with { Volume = volume, ErrorMessage = null });
    }

    public async Task<PlaybackSnapshot> SetMutedAsync(bool isMuted, CancellationToken cancellationToken = default)
    {
        return await RunEngineCommandAsync(
            engine => engine.SetMutedAsync(isMuted, cancellationToken),
            () => snapshot with { IsMuted = isMuted, ErrorMessage = null });
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
                source.Reason ?? "Playback source is not available.",
                snapshot.Position,
                snapshot.Volume,
                snapshot.IsMuted);
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
                "Playback source must be a local file path.",
                snapshot.Position,
                snapshot.Volume,
                snapshot.IsMuted);
            return snapshot;
        }

        snapshot = new PlaybackSnapshot(
            PlaybackState.Loading,
            source.CanonicalTrackId,
            source.PlaylistItemId,
            source.LocalMediaFileId,
            source.Path,
            null,
            TimeSpan.Zero,
            snapshot.Volume,
            snapshot.IsMuted);

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

    private async Task<PlaybackSnapshot> RunEngineCommandAsync(
        Func<IMediaEngine, Task> command,
        Func<PlaybackSnapshot> success)
    {
        try
        {
            await command(mediaEngine);
            snapshot = success();
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
