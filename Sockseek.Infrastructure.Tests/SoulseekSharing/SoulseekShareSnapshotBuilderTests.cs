using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.SoulseekSharing;

namespace Sockseek.Infrastructure.Tests.SoulseekSharing;

[TestClass]
public sealed class SoulseekShareSnapshotBuilderTests
{
    [TestMethod]
    public void Build_EnabledReadableRootIncludesOnlyCompletedAudioFilesWithTruthfulCounts()
    {
        using var root = TemporaryDirectory.Create();
        CreateFile(root.Path, "Artist", "Album", "one.mp3");
        CreateFile(root.Path, "Artist", "Album", "two.flac");
        CreateFile(root.Path, "Artist", "Album", "partial.mp3.incomplete");
        CreateFile(root.Path, "Artist", "notes.txt");
        var configured = Root(root.Path, "Music");

        var result = new SoulseekShareSnapshotBuilder().Build([configured]);

        Assert.AreEqual(1, result.Snapshot.Roots.Count);
        Assert.AreEqual(2, result.Snapshot.FileCount);
        Assert.AreEqual(3, result.Snapshot.DirectoryCount);
        CollectionAssert.AreEqual(
            new[] { "@@Music", "@@Music\\Artist", "@@Music\\Artist\\Album" },
            result.Snapshot.Directories.ToArray());
        CollectionAssert.AreEqual(
            new[] { "@@Music\\Artist\\Album\\one.mp3", "@@Music\\Artist\\Album\\two.flac" },
            result.Snapshot.Files.Select(file => file.RemoteFilename).ToArray());
        Assert.IsTrue(result.Roots.Single().Included);
        Assert.AreEqual(2, result.Roots.Single().FileCount);
    }

    [TestMethod]
    public void Build_DisabledAndMissingRootsAreNeverIncludedOrCounted()
    {
        using var root = TemporaryDirectory.Create();
        CreateFile(root.Path, "one.mp3");
        string missingPath = Path.Combine(root.Path, "missing");

        var result = new SoulseekShareSnapshotBuilder().Build(
        [
            Root(root.Path, "Disabled", enabled: false),
            Root(missingPath, "Missing"),
        ]);

        Assert.AreEqual(0, result.Snapshot.FileCount);
        Assert.AreEqual(0, result.Snapshot.DirectoryCount);
        Assert.AreEqual("disabled", result.Roots.Single(status => status.VirtualName == "Disabled").ErrorCode);
        Assert.AreEqual("root_missing", result.Roots.Single(status => status.VirtualName == "Missing").ErrorCode);
    }

    [TestMethod]
    public void TryGetFile_NormalizesSeparatorsButRejectsTraversalAndUnknownPaths()
    {
        using var root = TemporaryDirectory.Create();
        string localPath = CreateFile(root.Path, "Album", "track.mp3");
        var snapshot = new SoulseekShareSnapshotBuilder().Build([Root(root.Path, "Music")]).Snapshot;

        Assert.IsTrue(snapshot.TryGetFile("@@Music/Album/track.mp3", out var sharedFile));
        Assert.AreEqual(Path.GetFullPath(localPath), sharedFile?.LocalPath);
        Assert.IsFalse(snapshot.TryGetFile("@@Music\\..\\outside.mp3", out _));
        Assert.IsFalse(snapshot.TryGetFile("@@Music\\Album\\.\\track.mp3", out _));
        Assert.IsFalse(snapshot.TryGetFile("C:\\Music\\track.mp3", out _));
        Assert.IsFalse(snapshot.TryGetFile("@@Music\\Album\\unknown.mp3", out _));
    }

    [TestMethod]
    public void Build_DuplicateVirtualNamesNeverMergeRoots()
    {
        using var first = TemporaryDirectory.Create();
        using var second = TemporaryDirectory.Create();
        CreateFile(first.Path, "first.mp3");
        CreateFile(second.Path, "second.mp3");

        var result = new SoulseekShareSnapshotBuilder().Build(
        [
            Root(first.Path, "Music"),
            Root(second.Path, "music"),
        ]);

        Assert.AreEqual(1, result.Snapshot.Roots.Count);
        Assert.AreEqual(1, result.Snapshot.FileCount);
        Assert.AreEqual("duplicate_virtual_name", result.Roots.Single(status => !status.Included).ErrorCode);
    }

    [TestMethod]
    public void Build_SymlinkedDirectoryOutsideRootIsNotShared()
    {
        using var root = TemporaryDirectory.Create();
        using var outside = TemporaryDirectory.Create();
        CreateFile(root.Path, "inside.mp3");
        CreateFile(outside.Path, "outside.mp3");
        string linkPath = Path.Combine(root.Path, "linked-outside");
        if (!TryCreateDirectorySymbolicLink(linkPath, outside.Path))
            return;

        var result = new SoulseekShareSnapshotBuilder().Build([Root(root.Path, "Music")]);

        Assert.AreEqual(1, result.Snapshot.FileCount);
        Assert.AreEqual("@@Music\\inside.mp3", result.Snapshot.Files.Single().RemoteFilename);
        Assert.IsFalse(result.Snapshot.Files.Any(file => file.LocalPath.StartsWith(outside.Path, PathComparison)));
    }

    [TestMethod]
    public void Build_SymlinkRootIsRejectedInsteadOfFollowingItsTarget()
    {
        using var parent = TemporaryDirectory.Create();
        using var target = TemporaryDirectory.Create();
        CreateFile(target.Path, "target.mp3");
        string linkPath = Path.Combine(parent.Path, "linked-root");
        if (!TryCreateDirectorySymbolicLink(linkPath, target.Path))
            return;

        var result = new SoulseekShareSnapshotBuilder().Build([Root(linkPath, "Music")]);

        Assert.AreEqual(0, result.Snapshot.FileCount);
        Assert.AreEqual("root_reparse_point", result.Roots.Single().ErrorCode);
    }

    [TestMethod]
    public void OpenRead_ValidSnapshotSupportsBoundedResumeOffset()
    {
        using var root = TemporaryDirectory.Create();
        string path = CreateFile(root.Path, "track.mp3");
        File.WriteAllBytes(path, [1, 2, 3, 4, 5]);
        var sharedFile = new SoulseekShareSnapshotBuilder()
            .Build([Root(root.Path, "Music")])
            .Snapshot.Files.Single();

        using var stream = SoulseekSharedFileAccess.OpenRead(sharedFile, offset: 2);

        Assert.AreEqual(2, stream.Position);
        Assert.AreEqual(3, stream.ReadByte());
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            SoulseekSharedFileAccess.OpenRead(sharedFile, offset: sharedFile.Size + 1));
    }

    [TestMethod]
    public void OpenRead_FileChangedAfterSnapshotIsRejected()
    {
        using var root = TemporaryDirectory.Create();
        string path = CreateFile(root.Path, "track.mp3");
        var sharedFile = new SoulseekShareSnapshotBuilder()
            .Build([Root(root.Path, "Music")])
            .Snapshot.Files.Single();
        File.WriteAllBytes(path, [1, 2, 3, 4, 5]);

        var exception = Assert.ThrowsException<SoulseekSharedFileUnavailableException>(() =>
            SoulseekSharedFileAccess.OpenRead(sharedFile));

        Assert.AreEqual("file_changed", exception.ErrorCode);
        Assert.IsFalse(exception.Message.Contains(path, StringComparison.Ordinal));
    }

    [TestMethod]
    public void OpenRead_DirectoryReplacedByOutsideSymlinkAfterSnapshotIsRejected()
    {
        using var root = TemporaryDirectory.Create();
        using var outside = TemporaryDirectory.Create();
        string albumPath = Path.Combine(root.Path, "Album");
        CreateFile(albumPath, "track.mp3");
        CreateFile(outside.Path, "track.mp3");
        var sharedFile = new SoulseekShareSnapshotBuilder()
            .Build([Root(root.Path, "Music")])
            .Snapshot.Files.Single();

        Directory.Delete(albumPath, recursive: true);
        if (!TryCreateDirectorySymbolicLink(albumPath, outside.Path))
            return;

        var exception = Assert.ThrowsException<SoulseekSharedFileUnavailableException>(() =>
            SoulseekSharedFileAccess.OpenRead(sharedFile));

        Assert.AreEqual("reparse_point", exception.ErrorCode);
    }

    [TestMethod]
    public void OpenRead_SiblingWithMatchingPathPrefixIsOutsideRoot()
    {
        using var parent = TemporaryDirectory.Create();
        string rootPath = Path.Combine(parent.Path, "music");
        string siblingPath = Path.Combine(parent.Path, "music-private");
        Directory.CreateDirectory(rootPath);
        string outsideFile = CreateFile(siblingPath, "outside.mp3");
        var sharedFile = new SoulseekSharedFile(
            Guid.NewGuid(),
            rootPath,
            "@@Music\\outside.mp3",
            outsideFile,
            new FileInfo(outsideFile).Length);

        var exception = Assert.ThrowsException<SoulseekSharedFileUnavailableException>(() =>
            SoulseekSharedFileAccess.OpenRead(sharedFile));

        Assert.AreEqual("outside_root", exception.ErrorCode);
    }

    [TestMethod]
    public void Build_CancelledScanDoesNotPublishPartialSnapshot()
    {
        using var root = TemporaryDirectory.Create();
        CreateFile(root.Path, "track.mp3");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsException<OperationCanceledException>(() =>
            new SoulseekShareSnapshotBuilder().Build([Root(root.Path, "Music")], cancellation.Token));
    }

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static SoulseekShareRootRecord Root(string path, string virtualName, bool enabled = true)
    {
        var now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        return new SoulseekShareRootRecord(Guid.NewGuid(), path, virtualName, enabled, now, now);
    }

    private static string CreateFile(string root, params string[] segments)
    {
        string path = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        return path;
    }

    private static bool TryCreateDirectorySymbolicLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sockseek-share-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
