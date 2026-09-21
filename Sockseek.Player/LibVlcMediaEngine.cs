using LibVLCSharp.Shared;

namespace Sockseek.Player;

public sealed class LibVlcMediaEngine : IMediaEngine, IDisposable
{
    private readonly object gate = new();
    private readonly LibVLC libVlc;
    private readonly MediaPlayer mediaPlayer;
    private Media? currentMedia;
    private bool disposed;

    public LibVlcMediaEngine()
        : this([])
    {
    }

    public LibVlcMediaEngine(IReadOnlyList<string> additionalOptions)
    {
        Core.Initialize();
        var options = new[] { "--no-video", "--quiet" }
            .Concat(additionalOptions)
            .ToArray();
        libVlc = new LibVLC(options);
        mediaPlayer = new MediaPlayer(libVlc);
    }

    public Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Media path is required.", nameof(path));

        var mediaUri = new Uri(Path.GetFullPath(path));
        lock (gate)
        {
            ThrowIfDisposed();
            currentMedia?.Dispose();
            currentMedia = new Media(libVlc, mediaUri);
            mediaPlayer.Media = currentMedia;
        }

        return Task.CompletedTask;
    }

    public Task PlayAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            ThrowIfDisposed();
            if (!mediaPlayer.Play())
                throw new InvalidOperationException("LibVLC failed to start playback.");
        }

        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            ThrowIfDisposed();
            mediaPlayer.Pause();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            ThrowIfDisposed();
            mediaPlayer.Stop();
        }

        return Task.CompletedTask;
    }

    public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (position < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(position), "Seek position cannot be negative.");

        lock (gate)
        {
            ThrowIfDisposed();
            mediaPlayer.Time = Convert.ToInt64(position.TotalMilliseconds);
        }

        return Task.CompletedTask;
    }

    public Task SetVolumeAsync(double volume, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!double.IsFinite(volume) || volume is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0 and 1.");

        lock (gate)
        {
            ThrowIfDisposed();
            mediaPlayer.Volume = (int)Math.Round(volume * 100, MidpointRounding.AwayFromZero);
        }

        return Task.CompletedTask;
    }

    public Task SetMutedAsync(bool isMuted, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            ThrowIfDisposed();
            mediaPlayer.Mute = isMuted;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;

            currentMedia?.Dispose();
            mediaPlayer.Dispose();
            libVlc.Dispose();
            disposed = true;
        }
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(disposed, this);
}
