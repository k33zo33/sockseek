using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.LocalLibrary;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.LocalLibrary;

[TestClass]
public class LocalMediaFileRelinkerTests
{
    [TestMethod]
    public async Task RelinkAsync_ExistingLocalMediaFile_UpdatesPathAndKeepsCanonicalTrack()
    {
        using var temp = TemporaryDirectory.Create();
        string newPath = CreateAudioFile(temp.Path, "Artist", "Track.flac");
        var metadataReader = new FakeMetadataReader();
        metadataReader.Set(newPath, new LocalAudioMetadata("Different Tag Artist", "Different Tag Title", 201000, null, null, "flac", 900, 48000, 24));

        await using var database = await TestDatabase.CreateAsync();
        Guid trackId;
        Guid fileId;
        await using (var context = new SockseekDbContext(database.Options))
        {
            trackId = await new CanonicalTrackStore(context).UpsertAsync(CreateTrack("Artist", "Track", "C:/Missing/Track.mp3", LocalMediaAvailability.Missing));
            var file = await context.LocalMediaFiles.SingleAsync();
            fileId = file.Id;
        }

        await using (var context = new SockseekDbContext(database.Options))
        {
            var result = await new LocalMediaFileRelinker(context, metadataReader).RelinkAsync(fileId, newPath);

            Assert.IsNotNull(result);
            Assert.AreEqual(fileId, result.LocalMediaFileId);
            Assert.AreEqual(trackId, result.CanonicalTrackId);
            Assert.AreEqual(NormalizePath(newPath), result.Path);
        }

        await using var verify = new SockseekDbContext(database.Options);
        var relinked = await verify.LocalMediaFiles.SingleAsync();
        Assert.AreEqual(trackId, relinked.CanonicalTrackId);
        Assert.AreEqual(NormalizePath(newPath), relinked.Path);
        Assert.AreEqual((int)LocalMediaAvailability.Available, relinked.Availability);
        Assert.AreEqual(201000, relinked.DurationMs);
        Assert.AreEqual("flac", relinked.Codec);
        Assert.AreEqual(900, relinked.Bitrate);
        Assert.AreEqual(48000, relinked.SampleRate);
        Assert.AreEqual(24, relinked.BitDepth);

        var track = await verify.CanonicalTracks.SingleAsync();
        Assert.AreEqual("Artist", track.Artist);
        Assert.AreEqual("Track", track.Title);
    }

    [TestMethod]
    public async Task RelinkAsync_PathAlreadyUsedByAnotherFile_Throws()
    {
        using var temp = TemporaryDirectory.Create();
        string targetPath = CreateAudioFile(temp.Path, "Artist", "Target.mp3");

        await using var database = await TestDatabase.CreateAsync();
        Guid sourceFileId;
        await using (var context = new SockseekDbContext(database.Options))
        {
            var store = new CanonicalTrackStore(context);
            await store.UpsertAsync(CreateTrack("Artist", "Source", "C:/Missing/Source.mp3", LocalMediaAvailability.Missing));
            await store.UpsertAsync(CreateTrack("Artist", "Target", targetPath, LocalMediaAvailability.Available));
            sourceFileId = (await context.LocalMediaFiles.SingleAsync(file => file.Path == "C:/Missing/Source.mp3")).Id;
        }

        await using (var context = new SockseekDbContext(database.Options))
        {
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                new LocalMediaFileRelinker(context, new FakeMetadataReader()).RelinkAsync(sourceFileId, targetPath));
        }
    }

    private static CanonicalTrackRecord CreateTrack(
        string artist,
        string title,
        string path,
        LocalMediaAvailability availability)
        => new(
            artist,
            title,
            180000,
            null,
            null,
            [],
            [new LocalMediaFileRecord(
                path,
                1234,
                new DateTimeOffset(2026, 9, 17, 20, 1, 0, TimeSpan.Zero),
                180000,
                "mp3",
                320,
                44100,
                16,
                availability)]);

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
                : new LocalAudioMetadata(null, null, null, null, null, null, null, null, null));
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
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sockseek-relink-" + Guid.NewGuid().ToString("N"));
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
