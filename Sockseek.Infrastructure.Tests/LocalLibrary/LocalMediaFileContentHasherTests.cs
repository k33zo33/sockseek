using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Common;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.LocalLibrary;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.LocalLibrary;

[TestClass]
public class LocalMediaFileContentHasherTests
{
    [TestMethod]
    public async Task ComputeMissingHashesAsync_AvailableFileWithoutHash_StoresSha256Metadata()
    {
        using var temp = TemporaryDirectory.Create();
        string path = Path.Combine(temp.Path, "track.flac");
        byte[] bytes = [1, 2, 3, 4, 5];
        await File.WriteAllBytesAsync(path, bytes);

        await using var database = await TestDatabase.CreateAsync();
        await using (var context = new SockseekDbContext(database.Options))
        {
            await new CanonicalTrackStore(context).UpsertAsync(CreateTrack(path, LocalMediaAvailability.Available));
        }

        var now = new DateTimeOffset(2026, 9, 18, 9, 30, 0, TimeSpan.Zero);
        await using (var context = new SockseekDbContext(database.Options))
        {
            var result = await new LocalMediaFileContentHasher(context, new FakeClock(now)).ComputeMissingHashesAsync();

            Assert.AreEqual(1, result.ConsideredFiles);
            Assert.AreEqual(1, result.HashedFiles);
            Assert.AreEqual(0, result.MissingFiles);
            Assert.AreEqual(0, result.FailedFiles);
        }

        await using var verify = new SockseekDbContext(database.Options);
        var file = await verify.LocalMediaFiles.SingleAsync();
        Assert.AreEqual(ExpectedSha256(bytes), file.ContentHash);
        Assert.AreEqual(LocalMediaFileContentHasher.Sha256Algorithm, file.ContentHashAlgorithm);
        Assert.AreEqual(now, file.ContentHashComputedAtUtc);
        Assert.AreEqual((int)LocalMediaAvailability.Available, file.Availability);
    }

    [TestMethod]
    public async Task ComputeMissingHashesAsync_AlreadyHashedFile_IsSkipped()
    {
        using var temp = TemporaryDirectory.Create();
        string path = Path.Combine(temp.Path, "track.flac");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);

        await using var database = await TestDatabase.CreateAsync();
        await using (var context = new SockseekDbContext(database.Options))
        {
            await new CanonicalTrackStore(context).UpsertAsync(CreateTrack(path, LocalMediaAvailability.Available));
            var file = await context.LocalMediaFiles.SingleAsync();
            file.ContentHash = "existing";
            file.ContentHashAlgorithm = LocalMediaFileContentHasher.Sha256Algorithm;
            file.ContentHashComputedAtUtc = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero);
            await context.SaveChangesAsync();
        }

        await using (var context = new SockseekDbContext(database.Options))
        {
            var result = await new LocalMediaFileContentHasher(context, new FakeClock(DateTimeOffset.UtcNow)).ComputeMissingHashesAsync();

            Assert.AreEqual(0, result.ConsideredFiles);
            Assert.AreEqual(0, result.HashedFiles);
        }
    }

    [TestMethod]
    public async Task ComputeMissingHashesAsync_MissingPhysicalFile_MarksLocalFileMissing()
    {
        using var temp = TemporaryDirectory.Create();
        string path = Path.Combine(temp.Path, "track.flac");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);

        await using var database = await TestDatabase.CreateAsync();
        await using (var context = new SockseekDbContext(database.Options))
        {
            await new CanonicalTrackStore(context).UpsertAsync(CreateTrack(path, LocalMediaAvailability.Available));
        }

        File.Delete(path);

        await using (var context = new SockseekDbContext(database.Options))
        {
            var result = await new LocalMediaFileContentHasher(context, new FakeClock(DateTimeOffset.UtcNow)).ComputeMissingHashesAsync();

            Assert.AreEqual(1, result.ConsideredFiles);
            Assert.AreEqual(0, result.HashedFiles);
            Assert.AreEqual(1, result.MissingFiles);
        }

        await using var verify = new SockseekDbContext(database.Options);
        var file = await verify.LocalMediaFiles.SingleAsync();
        Assert.AreEqual((int)LocalMediaAvailability.Missing, file.Availability);
        Assert.IsNull(file.ContentHash);
    }

    private static CanonicalTrackRecord CreateTrack(string path, LocalMediaAvailability availability)
        => new(
            "Artist",
            "Track",
            null,
            180000,
            null,
            null,
            [],
            [new LocalMediaFileRecord(
                path,
                new FileInfo(path).Length,
                new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero),
                180000,
                "flac",
                900,
                48000,
                24,
                availability)]);

    private static string ExpectedSha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
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
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sockseek-content-hash-" + Guid.NewGuid().ToString("N"));
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
