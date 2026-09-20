using Sockseek.Application.Playback;

namespace Sockseek.Player;

public sealed class PlaybackCoordinator
{
    private readonly IPlaybackSourceResolver sourceResolver;
    private readonly IMediaEngine mediaEngine;

    private PlaybackSnapshot snapshot = PlaybackSnapshot.Stopped;
    private List<PlaybackQueueItem> queueItems = [];
    private IReadOnlyList<int> playbackOrder = [];
    private int currentQueueIndex = -1;
    private PlaybackRepeatMode repeatMode = PlaybackRepeatMode.None;
    private bool shuffleEnabled;
    private int shuffleSeed;

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

    public PlaybackQueueSnapshot Queue => new(
        queueItems.ToArray(),
        currentQueueIndex,
        repeatMode,
        shuffleEnabled,
        shuffleSeed,
        playbackOrder.ToArray());

    public PlaybackQueueSnapshot SetQueue(
        IReadOnlyList<PlaybackQueueItem> items,
        int currentIndex = 0,
        PlaybackRepeatMode repeatMode = PlaybackRepeatMode.None,
        bool shuffleEnabled = false,
        int shuffleSeed = 0)
    {
        if (items.Count == 0)
        {
            if (currentIndex is not (-1 or 0))
                throw new ArgumentOutOfRangeException(nameof(currentIndex), "Empty queues must use current index -1 or 0.");
        }
        else if (currentIndex < 0 || currentIndex >= items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(currentIndex), "Current index must point to a queue item.");
        }

        queueItems = items.ToList();
        currentQueueIndex = queueItems.Count == 0 ? -1 : currentIndex;
        this.repeatMode = repeatMode;
        this.shuffleEnabled = shuffleEnabled;
        this.shuffleSeed = shuffleSeed;
        RebuildPlaybackOrder();
        return Queue;
    }

    public PlaybackQueueSnapshot SetRepeatMode(PlaybackRepeatMode repeatMode)
    {
        this.repeatMode = repeatMode;
        return Queue;
    }

    public PlaybackQueueSnapshot SetShuffle(bool enabled, int seed)
    {
        shuffleEnabled = enabled;
        shuffleSeed = seed;
        RebuildPlaybackOrder();
        return Queue;
    }

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

    public async Task<PlaybackSnapshot> PlayCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (currentQueueIndex < 0 || currentQueueIndex >= queueItems.Count)
            return await StopAsync(cancellationToken);

        return await PlayCanonicalTrackAsync(queueItems[currentQueueIndex].CanonicalTrackId, cancellationToken);
    }

    public async Task<PlaybackSnapshot> NextAsync(CancellationToken cancellationToken = default)
    {
        if (queueItems.Count == 0)
            return await StopAsync(cancellationToken);

        if (repeatMode == PlaybackRepeatMode.One && currentQueueIndex >= 0)
            return await PlayCurrentAsync(cancellationToken);

        var nextIndex = GetRelativeQueueIndex(1);
        if (nextIndex == null)
            return await StopAsync(cancellationToken);

        currentQueueIndex = nextIndex.Value;
        return await PlayCurrentAsync(cancellationToken);
    }

    public async Task<PlaybackSnapshot> PreviousAsync(CancellationToken cancellationToken = default)
    {
        if (queueItems.Count == 0)
            return snapshot;

        if (repeatMode == PlaybackRepeatMode.One && currentQueueIndex >= 0)
            return await PlayCurrentAsync(cancellationToken);

        var previousIndex = GetRelativeQueueIndex(-1);
        if (previousIndex == null)
            return snapshot;

        currentQueueIndex = previousIndex.Value;
        return await PlayCurrentAsync(cancellationToken);
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

    private int? GetRelativeQueueIndex(int delta)
    {
        if (playbackOrder.Count != queueItems.Count)
            RebuildPlaybackOrder();

        if (playbackOrder.Count == 0)
            return null;

        if (currentQueueIndex < 0)
            return delta > 0 ? playbackOrder[0] : null;

        var currentOrderIndex = IndexOf(playbackOrder, currentQueueIndex);
        if (currentOrderIndex < 0)
            currentOrderIndex = 0;

        var targetOrderIndex = currentOrderIndex + delta;
        if (targetOrderIndex >= 0 && targetOrderIndex < playbackOrder.Count)
            return playbackOrder[targetOrderIndex];

        return repeatMode == PlaybackRepeatMode.All
            ? playbackOrder[(targetOrderIndex + playbackOrder.Count) % playbackOrder.Count]
            : null;
    }

    private void RebuildPlaybackOrder()
    {
        if (queueItems.Count == 0)
        {
            playbackOrder = [];
            currentQueueIndex = -1;
            return;
        }

        if (currentQueueIndex < 0 || currentQueueIndex >= queueItems.Count)
            currentQueueIndex = 0;

        var order = Enumerable.Range(0, queueItems.Count)
            .OrderBy(index => shuffleEnabled ? StableShuffleKey(shuffleSeed, index) : (uint)index)
            .ThenBy(index => index)
            .ToList();

        if (shuffleEnabled)
        {
            order.Remove(currentQueueIndex);
            order.Insert(0, currentQueueIndex);
        }

        playbackOrder = order;
    }

    private static int IndexOf(IReadOnlyList<int> items, int value)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] == value)
                return i;
        }

        return -1;
    }

    private static uint StableShuffleKey(int seed, int index)
    {
        unchecked
        {
            var value = (uint)seed ^ ((uint)index * 0x9E3779B9u);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
