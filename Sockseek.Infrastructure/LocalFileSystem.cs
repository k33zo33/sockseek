using Sockseek.Application.Common;

namespace Sockseek.Infrastructure;

public sealed class LocalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public Stream OpenRead(string path) => File.OpenRead(path);
}
