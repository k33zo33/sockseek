using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Common;
using Sockseek.Infrastructure.LocalLibrary;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.LocalLibrary;

[TestClass]
public class LocalLibraryScanCoordinatorTests
{
    [TestMethod]
    public async Task ScanEnabledRootsAsync_ScansEnabledRootsAndUpdatesCheckpoints()
    {
        using var enabledRoot = TemporaryDirectory.Create();
        using var disabledRoot = TemporaryDirectory.Create();
        string enabledFile = CreateAudioFile(enabledRoot.Path, "Artist", "Enabled.mp3");
        string disabledFile = CreateAudioFile(disabledRoot.Path, "Artist", "Disabled.mp3");
        var metadataReader = new FakeMetadataReader();
        metadataReader.Set(enabledFile, new LocalAudioMetadata("Artist", "Enabled", "Enabled Album", 180000, null, null, "mp3", 320, 44100, 16));
        metadataReader.Set(disabledFile, new LocalAudioMetadata("Artist", "Disabled", "Disabled Album", 180000, null, null, "mp3", 320, 44100, 16));

        await using var database = await TestDatabase.CreateAsync();
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 17, 22, 0, 0, TimeSpan.Zero));
        Guid enabledRootId;
        Guid disabledRootId;
        var progressSnapshots = new List<LocalLibraryScanProgress>();

        await using (var context = new SockseekDbContext(database.Options))
        {
            var rootStore = new LibraryRootStore(context, clock);
            enabledRootId = await rootStore.AddOrUpdateAsync(enabledRoot.Path, "Enabled root");
            disabledRootId = await rootStore.AddOrUpdateAsync(disabledRoot.Path, "Disabled root", enabled: false);

            var scanner = new LocalLibraryScanner(context, new CanonicalTrackStore(context), metadataReader);
            var coordinator = new LocalLibraryScanCoordinator(rootStore, scanner);

            var result = await coordinator.ScanEnabledRootsAsync(new Progress<LocalLibraryScanProgress>(progressSnapshots.Add));

            CollectionAssert.AreEqual(new[] { enabledRootId }, result.RootIds.ToArray());
            Assert.AreEqual(1, result.ScanResult.DiscoveredFiles);
            Assert.AreEqual(1, result.ScanResult.ImportedFiles);
            Assert.IsTrue(progressSnapshots.Count > 0);
        }

        await using var verify = new SockseekDbContext(database.Options);
        Assert.AreEqual(1, await verify.CanonicalTracks.CountAsync());
        Assert.AreEqual(1, await verify.LocalMediaFiles.CountAsync());
        var track = await verify.CanonicalTracks.SingleAsync();
        Assert.AreEqual("Enabled", track.Title);
        Assert.AreEqual("Enabled Album", track.AlbumTitle);

        var enabled = await verify.LibraryRoots.SingleAsync(root => root.Id == enabledRootId);
        Assert.IsNotNull(enabled.LastScanStartedUtc);
        Assert.IsNotNull(enabled.LastScanCompletedUtc);

        var disabled = await verify.LibraryRoots.SingleAsync(root => root.Id == disabledRootId);
        Assert.IsNull(disabled.LastScanStartedUtc);
        Assert.IsNull(disabled.LastScanCompletedUtc);
    }

    [TestMethod]
    public async Task ScanEnabledRootsAsync_NoEnabledRoots_ReturnsEmptyResult()
    {
        using var disabledRoot = TemporaryDirectory.Create();
        await using var database = await TestDatabase.CreateAsync();
        await using var context = new SockseekDbContext(database.Options);
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 17, 22, 0, 0, TimeSpan.Zero));
        var rootStore = new LibraryRootStore(context, clock);
        await rootStore.AddOrUpdateAsync(disabledRoot.Path, enabled: false);

        var scanner = new LocalLibraryScanner(context, new CanonicalTrackStore(context), new FakeMetadataReader());
        var coordinator = new LocalLibraryScanCoordinator(rootStore, scanner);

        var result = await coordinator.ScanEnabledRootsAsync();

        Assert.AreEqual(0, result.RootIds.Count);
        Assert.AreEqual(0, result.ScanResult.DiscoveredFiles);
    }

    private static string CreateAudioFile(string root, params string[] segments)
    {
        string path = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1, 2, 3, 4, 5]);
        return path;
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).Trim().Replace('\\', '/');

    private sealed class FakeMetadataReader : ILocalAudioMetadataReader
    {
        private readonly Dictionary<string, LocalAudioMetadata> metadataByPath = new(StringComparer.OrdinalIgnoreCase);

        public void Set(string path, LocalAudioMetadata metadata)
            => metadataByPath[NormalizePath(path)] = metadata;

        public Task<LocalAudioMetadata> ReadAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult(metadataByPath.TryGetValue(NormalizePath(path), out var metadata)
                ? metadata
                : new LocalAudioMetadata(null, null, null, null, null, null, null, null, null, null));
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestDatabase(SqliteConnection connection, DbContextOptions<SockseekDbContext> options)
        {
            this.connection = connection;
            Options = options;
        }

        public DbContextOptions<SockseekDbContext> Options { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<SockseekDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var setup = new SockseekDbContext(options);
            await setup.Database.MigrateAsync();

            return new TestDatabase(connection, options);
        }

        public ValueTask DisposeAsync()
            => connection.DisposeAsync();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
            => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sockseek-local-library-" + Guid.NewGuid().ToString("N"));
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
