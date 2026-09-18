namespace Sockseek.Player;

public sealed class UnavailableMediaEngine : IMediaEngine
{
    public Task LoadAsync(string path, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("A media engine has not been configured.");

    public Task PlayAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PauseAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
