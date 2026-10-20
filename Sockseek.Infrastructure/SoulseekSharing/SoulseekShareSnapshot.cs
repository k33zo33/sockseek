using System.Collections.ObjectModel;

namespace Sockseek.Infrastructure.SoulseekSharing;

public sealed record SoulseekSharedFile(
    Guid RootId,
    string RootPath,
    string RemoteFilename,
    string LocalPath,
    long Size);

public sealed record SoulseekShareRootSnapshot(
    Guid Id,
    string VirtualName,
    int DirectoryCount,
    int FileCount);

public sealed record SoulseekShareRootBuildStatus(
    Guid Id,
    string VirtualName,
    bool Enabled,
    bool Included,
    string? ErrorCode,
    int DirectoryCount,
    int FileCount);

public sealed class SoulseekShareSnapshot
{
    private readonly IReadOnlyDictionary<string, SoulseekSharedFile> filesByRemoteFilename;

    public static SoulseekShareSnapshot Empty { get; } = new([], [], []);

    public SoulseekShareSnapshot(
        IReadOnlyList<SoulseekShareRootSnapshot> roots,
        IReadOnlyList<string> directories,
        IReadOnlyList<SoulseekSharedFile> files)
    {
        Roots = roots;
        Directories = directories;
        Files = files;
        filesByRemoteFilename = new ReadOnlyDictionary<string, SoulseekSharedFile>(
            files.ToDictionary(file => file.RemoteFilename, StringComparer.Ordinal));
    }

    public IReadOnlyList<SoulseekShareRootSnapshot> Roots { get; }

    public IReadOnlyList<string> Directories { get; }

    public IReadOnlyList<SoulseekSharedFile> Files { get; }

    public int DirectoryCount => Directories.Count;

    public int FileCount => Files.Count;

    public bool TryGetFile(string remoteFilename, out SoulseekSharedFile? file)
    {
        file = null;
        return SoulseekRemotePath.TryNormalize(remoteFilename, out string normalized)
            && filesByRemoteFilename.TryGetValue(normalized, out file);
    }
}

public sealed record SoulseekShareBuildResult(
    SoulseekShareSnapshot Snapshot,
    IReadOnlyList<SoulseekShareRootBuildStatus> Roots);
