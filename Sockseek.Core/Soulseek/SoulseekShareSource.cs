namespace Sockseek.Core.Services;

public sealed record SoulseekShareSourceFile(
    string RemoteFilename,
    long Size,
    string Extension);

public sealed record SoulseekShareSourceSnapshot(
    long Revision,
    int DirectoryCount,
    IReadOnlyList<SoulseekShareSourceFile> Files)
{
    public static SoulseekShareSourceSnapshot Empty { get; } = new(0, 0, []);

    public int FileCount => Files.Count;
}

public interface ISoulseekShareSource
{
    event EventHandler? SnapshotChanged;

    SoulseekShareSourceSnapshot GetSnapshot();

    Task<Stream> OpenReadAsync(
        string remoteFilename,
        long offset,
        CancellationToken cancellationToken = default);
}
