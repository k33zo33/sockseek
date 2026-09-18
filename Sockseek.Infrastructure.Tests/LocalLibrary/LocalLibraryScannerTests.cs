using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.LocalLibrary;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.LocalLibrary;

[TestClass]
public class LocalLibraryScannerTests
{
    [TestMethod]
    public async Task ScanAsync_TempDirectory_ImportsSupportedAudioMetadata()
    {
        using var temp = TemporaryDirectory.Create();
        string filePath = CreateAudioFile(temp.Path, "Artist", "Track.mp3");
        var metadataReader = new FakeMetadataReader();
        metadataReader.Set(filePath, new LocalAudioMetadata("Artist", "Track", 181000, "hr-abc-1", null, "mp3", 320, 44100, 16));

        await using var database = await TestDatabase.CreateAsync();
        var scanner = CreateScanner(database.Options, metadataReader);

        var result = await scanner.ScanAsync(new LocalLibraryScanRequest([temp.Path]));

        Assert.AreEqual(1, result.DiscoveredFiles);
        Assert.AreEqual(1, result.ImportedFiles);
        Assert.AreEqual(0, result.FailedFiles);

        await using var verify = new SockseekDbContext(database.Options);
        var track = await verify.CanonicalTracks.Include(x => x.LocalMediaFiles).SingleAsync();
        Assert.AreEqual("Artist", track.Artist);
        Assert.AreEqual("Track", track.Title);
        Assert.AreEqual("HR-ABC-1", track.Isrc);

        var localFile = track.LocalMediaFiles.Single();
        Assert.AreEqual(NormalizePath(filePath), localFile.Path);
        Assert.AreEqual((int)LocalMediaAvailability.Available, localFile.Availability);
        Assert.AreEqual("mp3", localFile.Codec);
        Assert.AreEqual(320, localFile.Bitrate);
    }

    [TestMethod]
    public async Task ScanAsync_RepeatedScan_DoesNotDuplicateTrackOrLocalMediaFile()
    {
        using var temp = TemporaryDirectory.Create();
        string filePath = CreateAudioFile(temp.Path, "Artist", "Track.flac");
        var metadataReader = new FakeMetadataReader();
        metadataReader.Set(filePath, new LocalAudioMetadata("Artist", "Track", 220000, null, "recording-a", "flac", 900, 48000, 24));

        await using var database = await TestDatabase.CreateAsync();
        var scanner = CreateScanner(database.Options, metadataReader);

        await scanner.ScanAsync(new LocalLibraryScanRequest([temp.Path]));
        await scanner.ScanAsync(new LocalLibraryScanRequest([temp.Path]));

        await using var verify = new SockseekDbContext(database.Options);
        Assert.AreEqual(1, await verify.CanonicalTracks.CountAsync());
        Assert.AreEqual(1, await verify.LocalMediaFiles.CountAsync());
        Assert.AreEqual((int)LocalMediaAvailability.Available, (await verify.LocalMediaFiles.SingleAsync()).Availability);
    }

    [TestMethod]
    public async Task ScanAsync_MultipleFilesForSameTrack_ImportsDuplicateLocalFiles()
    {
        using var temp = TemporaryDirectory.Create();
        string firstPath = CreateAudioFile(temp.Path, "A", "Track.flac");
        string secondPath = CreateAudioFile(temp.Path, "B", "Track.flac");
        var metadataReader = new FakeMetadataReader();
        metadataReader.Set(firstPath, new LocalAudioMetadata("Artist", "Track", 220000, null, null, "flac", 900, 48000, 24));
        metadataReader.Set(secondPath, new LocalAudioMetadata("Artist", "Track", 220000, null, null, "flac", 900, 48000, 24));

        await using var database = await TestDatabase.CreateAsync();
        var scanner = CreateScanner(database.Options, metadataReader);

        var result = await scanner.ScanAsync(new LocalLibraryScanRequest([temp.Path]));

        Assert.AreEqual(2, result.DiscoveredFiles);
        Assert.AreEqual(2, result.ImportedFiles);
        Assert.AreEqual(0, result.FailedFiles);

        await using var verify = new SockseekDbContext(database.Options);
        var track = await verify.CanonicalTracks.Include(x => x.LocalMediaFiles).SingleAsync();
        Assert.AreEqual(2, track.LocalMediaFiles.Count);
    }

    [TestMethod]
    public async Task ScanAsync_ChangedTags_RefreshesLocalMediaAndCanonicalLink()
    {
        using var temp = TemporaryDirectory.Create();
        string filePath = CreateAudioFile(temp.Path, "Artist", "Track.mp3");
        var metadataReader = new FakeMetadataReader();
        metadataReader.Set(filePath, new LocalAudioMetadata("Artist", "Old Title", 200000, null, null, "mp3", 128, 44100, 16));

        await using var database = await TestDatabase.CreateAsync();
        var scanner = CreateScanner(database.Options, metadataReader);

        await scanner.ScanAsync(new LocalLibraryScanRequest([temp.Path]));
        metadataReader.Set(filePath, new LocalAudioMetadata("Artist", "New Title", 201000, null, null, "mp3", 320, 48000, 24));
        await scanner.ScanAsync(new LocalLibraryScanRequest([temp.Path]));

        await using var verify = new SockseekDbContext(database.Options);
        Assert.AreEqual(1, await verify.LocalMediaFiles.CountAsync());

        var localFile = await verify.LocalMediaFiles.Include(x => x.CanonicalTrack).SingleAsync();
        Assert.AreEqual("New Title", localFile.CanonicalTrack?.Title);
        Assert.AreEqual(201000, localFile.DurationMs);
        Assert.AreEqual(320, localFile.Bitrate);
        Assert.AreEqual(48000, localFile.SampleRate);
        Assert.AreEqual(24, localFile.BitDepth);
        Assert.AreEqual((int)LocalMediaAvailability.Available, localFile.Availability);
    }

    [TestMethod]
    public async Task ScanAsync_DeletedFile_MarksMissingWithoutDeletingTrackIdentity()
    {
        using var temp = TemporaryDirectory.Create();
        string filePath = CreateAudioFile(temp.Path, "Artist", "Track.mp3");
        var metadataReader = new FakeMetadataReader();
        metadataReader.Set(filePath, new LocalAudioMetadata("Artist", "Track", 180000, null, null, "mp3", 256, 44100, 16));

        await using var database = await TestDatabase.CreateAsync();
        var scanner = CreateScanner(database.Options, metadataReader);

        await scanner.ScanAsync(new LocalLibraryScanRequest([temp.Path]));
        File.Delete(filePath);
        var result = await scanner.ScanAsync(new LocalLibraryScanRequest([temp.Path]));

        Assert.AreEqual(1, result.MissingFiles);

        await using var verify = new SockseekDbContext(database.Options);
        Assert.AreEqual(1, await verify.CanonicalTracks.CountAsync());
        Assert.AreEqual(1, await verify.LocalMediaFiles.CountAsync());
        Assert.AreEqual((int)LocalMediaAvailability.Missing, (await verify.LocalMediaFiles.SingleAsync()).Availability);
    }

    [TestMethod]
    public async Task ScanAsync_UnsupportedFiles_AreIgnored()
    {
        using var temp = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "notes.txt"), "not audio");

        await using var database = await TestDatabase.CreateAsync();
        var scanner = CreateScanner(database.Options, new FakeMetadataReader());

        var result = await scanner.ScanAsync(new LocalLibraryScanRequest([temp.Path]));

        Assert.AreEqual(0, result.DiscoveredFiles);
        Assert.AreEqual(0, result.ImportedFiles);

        await using var verify = new SockseekDbContext(database.Options);
        Assert.AreEqual(0, await verify.CanonicalTracks.CountAsync());
        Assert.AreEqual(0, await verify.LocalMediaFiles.CountAsync());
    }

    [TestMethod]
    public async Task ScanAsync_MetadataFailure_ContinuesAndReportsProgress()
    {
        using var temp = TemporaryDirectory.Create();
        string goodPath = CreateAudioFile(temp.Path, "Artist", "Good.mp3");
        string badPath = CreateAudioFile(temp.Path, "Artist", "Bad.mp3");
        var metadataReader = new FakeMetadataReader();
        metadataReader.Set(goodPath, new LocalAudioMetadata("Artist", "Good", 180000, null, null, "mp3", 320, 44100, 16));
        metadataReader.ThrowOn(badPath);

        await using var database = await TestDatabase.CreateAsync();
        var scanner = CreateScanner(database.Options, metadataReader);
        var progressSnapshots = new List<LocalLibraryScanProgress>();

        var result = await scanner.ScanAsync(
            new LocalLibraryScanRequest([temp.Path]),
            new Progress<LocalLibraryScanProgress>(progressSnapshots.Add));

        Assert.AreEqual(2, result.DiscoveredFiles);
        Assert.AreEqual(1, result.ImportedFiles);
        Assert.AreEqual(1, result.FailedFiles);
        Assert.AreEqual(2, progressSnapshots.Last().ScannedFiles);
        Assert.AreEqual(1, progressSnapshots.Last().FailedFiles);

        await using var verify = new SockseekDbContext(database.Options);
        Assert.AreEqual(1, await verify.CanonicalTracks.CountAsync());
        Assert.AreEqual(1, await verify.LocalMediaFiles.CountAsync());
    }

    private static LocalLibraryScanner CreateScanner(DbContextOptions<SockseekDbContext> options, ILocalAudioMetadataReader metadataReader)
    {
        var context = new SockseekDbContext(options);
        return new LocalLibraryScanner(context, new CanonicalTrackStore(context), metadataReader);
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
        private readonly HashSet<string> failingPaths = new(StringComparer.OrdinalIgnoreCase);

        public void Set(string path, LocalAudioMetadata metadata)
            => metadataByPath[NormalizePath(path)] = metadata;

        public void ThrowOn(string path)
            => failingPaths.Add(NormalizePath(path));

        public Task<LocalAudioMetadata> ReadAsync(string path, CancellationToken cancellationToken = default)
        {
            string normalizedPath = NormalizePath(path);
            if (failingPaths.Contains(normalizedPath))
                throw new InvalidDataException("Invalid test metadata.");

            return Task.FromResult(metadataByPath.TryGetValue(normalizedPath, out var metadata)
                ? metadata
                : new LocalAudioMetadata(null, null, null, null, null, null, null, null, null));
        }
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
