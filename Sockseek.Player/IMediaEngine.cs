namespace Sockseek.Player;

public interface IMediaEngine
{
    Task LoadAsync(string path, CancellationToken cancellationToken = default);

    Task PlayAsync(CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);

    Task SetVolumeAsync(double volume, CancellationToken cancellationToken = default);

    Task SetMutedAsync(bool isMuted, CancellationToken cancellationToken = default);
}
