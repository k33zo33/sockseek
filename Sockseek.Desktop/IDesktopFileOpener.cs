namespace Sockseek.Desktop;

public interface IDesktopFileOpener
{
    Task<bool> OpenFileAsync(string path, CancellationToken cancellationToken = default);

    Task<bool> OpenFolderAsync(string path, CancellationToken cancellationToken = default);
}
