namespace Sockseek.Application.Common;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    Stream OpenRead(string path);
}
