using System.Diagnostics;

namespace Sockseek.Desktop;

public sealed class SystemDesktopFileOpener : IDesktopFileOpener
{
    public Task<bool> OpenFileAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Task.FromResult(false);

        return Task.FromResult(OpenShellPath(Path.GetFullPath(path)));
    }

    public Task<bool> OpenFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(false);

        var fullPath = Path.GetFullPath(path);
        var folder = Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return Task.FromResult(false);

        return Task.FromResult(OpenShellPath(folder));
    }

    private static bool OpenShellPath(string path)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
        return process != null;
    }
}
