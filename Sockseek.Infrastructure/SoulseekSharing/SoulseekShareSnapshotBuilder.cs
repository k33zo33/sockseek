using Sockseek.Infrastructure.LocalLibrary;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.SoulseekSharing;

public sealed class SoulseekShareSnapshotBuilder
{
    private readonly IReadOnlySet<string> supportedExtensions;

    public SoulseekShareSnapshotBuilder(IReadOnlySet<string>? supportedExtensions = null)
    {
        this.supportedExtensions = supportedExtensions ?? LocalLibraryScanner.DefaultSupportedExtensions;
    }

    public SoulseekShareBuildResult Build(
        IReadOnlyList<SoulseekShareRootRecord> configuredRoots,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuredRoots);

        var files = new List<SoulseekSharedFile>();
        var directories = new HashSet<string>(StringComparer.Ordinal);
        var roots = new List<SoulseekShareRootSnapshot>();
        var statuses = new List<SoulseekShareRootBuildStatus>(configuredRoots.Count);
        var remoteFiles = new HashSet<string>(StringComparer.Ordinal);
        var virtualNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configuredRoot in configuredRoots.OrderBy(root => root.VirtualName, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!configuredRoot.Enabled)
            {
                statuses.Add(Status(configuredRoot, included: false, "disabled"));
                continue;
            }

            if (!virtualNames.Add(configuredRoot.VirtualName))
            {
                statuses.Add(Status(configuredRoot, included: false, "duplicate_virtual_name"));
                continue;
            }

            BuildRoot(configuredRoot, files, directories, roots, statuses, remoteFiles, cancellationToken);
        }

        return new SoulseekShareBuildResult(
            new SoulseekShareSnapshot(
                roots.ToArray(),
                directories.Order(StringComparer.Ordinal).ToArray(),
                files.OrderBy(file => file.RemoteFilename, StringComparer.Ordinal).ToArray()),
            statuses.ToArray());
    }

    private void BuildRoot(
        SoulseekShareRootRecord configuredRoot,
        List<SoulseekSharedFile> files,
        HashSet<string> directories,
        List<SoulseekShareRootSnapshot> roots,
        List<SoulseekShareRootBuildStatus> statuses,
        HashSet<string> remoteFiles,
        CancellationToken cancellationToken)
    {
        string rootPath;
        DirectoryInfo rootInfo;
        try
        {
            rootPath = SoulseekShareRootStore.NormalizeDirectoryPath(configuredRoot.Path);
            rootInfo = new DirectoryInfo(rootPath);
            if (!rootInfo.Exists)
            {
                statuses.Add(Status(configuredRoot, included: false, "root_missing"));
                return;
            }

            if (IsReparsePoint(rootInfo.Attributes))
            {
                statuses.Add(Status(configuredRoot, included: false, "root_reparse_point"));
                return;
            }
        }
        catch (Exception exception) when (IsFilesystemFailure(exception))
        {
            statuses.Add(Status(configuredRoot, included: false, "root_unreadable"));
            return;
        }

        int startingFileCount = files.Count;
        var rootDirectories = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };

            foreach (string candidate in Directory.EnumerateFiles(rootPath, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!supportedExtensions.Contains(Path.GetExtension(candidate))
                    || LocalLibraryScanner.IsIncompleteFilePath(candidate))
                {
                    continue;
                }

                if (!TryCreateSharedFile(configuredRoot, rootPath, candidate, out var sharedFile)
                    || !remoteFiles.Add(sharedFile.RemoteFilename))
                {
                    continue;
                }

                files.Add(sharedFile);
                AddRemoteDirectories(configuredRoot.VirtualName, sharedFile.RemoteFilename, rootDirectories);
            }
        }
        catch (Exception exception) when (IsFilesystemFailure(exception))
        {
            files.RemoveRange(startingFileCount, files.Count - startingFileCount);
            foreach (string remoteFile in remoteFiles.Where(path => path.StartsWith(
                SoulseekRemotePath.Root(configuredRoot.VirtualName) + "\\",
                StringComparison.Ordinal)).ToArray())
            {
                remoteFiles.Remove(remoteFile);
            }
            statuses.Add(Status(configuredRoot, included: false, "scan_failed"));
            return;
        }

        directories.UnionWith(rootDirectories);
        int rootFileCount = files.Count - startingFileCount;
        roots.Add(new SoulseekShareRootSnapshot(
            configuredRoot.Id,
            configuredRoot.VirtualName,
            rootDirectories.Count,
            rootFileCount));
        statuses.Add(Status(
            configuredRoot,
            included: true,
            errorCode: null,
            rootDirectories.Count,
            rootFileCount));
    }

    private static bool TryCreateSharedFile(
        SoulseekShareRootRecord configuredRoot,
        string rootPath,
        string candidate,
        out SoulseekSharedFile sharedFile)
    {
        sharedFile = null!;
        try
        {
            string fullPath = Path.GetFullPath(candidate);
            if (!IsContained(rootPath, fullPath))
                return false;

            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists || IsReparsePoint(fileInfo.Attributes))
                return false;

            string relativePath = Path.GetRelativePath(rootPath, fullPath);
            if (relativePath.Length == 0 || relativePath == "." || Path.IsPathRooted(relativePath))
                return false;

            string remoteFilename = SoulseekRemotePath.Combine(configuredRoot.VirtualName, relativePath);
            if (!SoulseekRemotePath.TryNormalize(remoteFilename, out remoteFilename))
                return false;

            sharedFile = new SoulseekSharedFile(
                configuredRoot.Id,
                rootPath,
                remoteFilename,
                fullPath,
                fileInfo.Length);
            return true;
        }
        catch (Exception exception) when (IsFilesystemFailure(exception))
        {
            return false;
        }
    }

    private static void AddRemoteDirectories(
        string virtualName,
        string remoteFilename,
        HashSet<string> directories)
    {
        string root = SoulseekRemotePath.Root(virtualName);
        directories.Add(root);

        int separator = remoteFilename.IndexOf('\\', root.Length + 1);
        while (separator >= 0)
        {
            directories.Add(remoteFilename[..separator]);
            separator = remoteFilename.IndexOf('\\', separator + 1);
        }
    }

    private static bool IsContained(string rootPath, string candidatePath)
    {
        string normalizedRoot = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string normalizedCandidate = Path.GetFullPath(candidatePath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return normalizedCandidate.StartsWith(normalizedRoot, comparison);
    }

    private static bool IsReparsePoint(FileAttributes attributes)
        => (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;

    private static bool IsFilesystemFailure(Exception exception)
        => exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException;

    private static SoulseekShareRootBuildStatus Status(
        SoulseekShareRootRecord root,
        bool included,
        string? errorCode,
        int directoryCount = 0,
        int fileCount = 0)
        => new(
            root.Id,
            root.VirtualName,
            root.Enabled,
            included,
            errorCode,
            directoryCount,
            fileCount);
}
