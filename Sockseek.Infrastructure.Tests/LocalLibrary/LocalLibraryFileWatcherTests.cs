using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Infrastructure.LocalLibrary;

namespace Sockseek.Infrastructure.Tests.LocalLibrary;

[TestClass]
public class LocalLibraryFileWatcherTests
{
    [TestMethod]
    public async Task Watcher_DetectsMovedAndDeletedSupportedFiles()
    {
        using var temp = TemporaryDirectory.Create();
        string originalPath = Path.Combine(temp.Path, "Artist", "Track.mp3");
        string movedPath = Path.Combine(temp.Path, "Artist", "Track moved.mp3");
        Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
        File.WriteAllBytes(originalPath, [1, 2, 3]);

        var changes = new List<LocalLibraryFileChange>();
        using var watcher = new LocalLibraryFileWatcher();
        watcher.Changed += (_, change) =>
        {
            lock (changes)
                changes.Add(change);
        };
        watcher.Start([temp.Path]);

        File.Move(originalPath, movedPath);
        await WaitForAsync(changes, change =>
            change.Kind == LocalLibraryFileChangeKind.Renamed
            && change.OldPath == NormalizePath(originalPath)
            && change.Path == NormalizePath(movedPath));

        File.Delete(movedPath);
        await WaitForAsync(changes, change =>
            change.Kind == LocalLibraryFileChangeKind.Deleted
            && change.Path == NormalizePath(movedPath));
    }

    [TestMethod]
    public async Task Watcher_IgnoresUnsupportedFiles()
    {
        using var temp = TemporaryDirectory.Create();

        var changes = new List<LocalLibraryFileChange>();
        using var watcher = new LocalLibraryFileWatcher();
        watcher.Changed += (_, change) =>
        {
            lock (changes)
                changes.Add(change);
        };
        watcher.Start([temp.Path]);

        string textPath = Path.Combine(temp.Path, "notes.txt");
        await File.WriteAllTextAsync(textPath, "not audio");
        await Task.Delay(300);

        lock (changes)
            Assert.AreEqual(0, changes.Count);
    }

    private static async Task WaitForAsync(
        List<LocalLibraryFileChange> changes,
        Func<LocalLibraryFileChange, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested)
        {
            lock (changes)
            {
                if (changes.Any(predicate))
                    return;
            }

            await Task.Delay(50, timeout.Token).ContinueWith(_ => { });
        }

        lock (changes)
        {
            Assert.Fail("Expected file watcher event was not observed. Events: "
                + string.Join(", ", changes.Select(change => $"{change.Kind}:{change.OldPath}->{change.Path}")));
        }
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).Trim().Replace('\\', '/');

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
            => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sockseek-library-watch-" + Guid.NewGuid().ToString("N"));
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
