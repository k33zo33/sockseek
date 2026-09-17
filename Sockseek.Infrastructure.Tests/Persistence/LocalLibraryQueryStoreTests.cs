using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public class LocalLibraryQueryStoreTests
{
    [TestMethod]
    public async Task SearchAsync_FindsAvailableAndMissingTracks_WithPaging()
    {
        await using var database = await TestDatabase.CreateAsync();

        Guid availableId;
        await using (var context = new SockseekDbContext(database.Options))
        {
            var store = new CanonicalTrackStore(context);
            availableId = await store.UpsertAsync(CreateTrack("The Artist", "First Song", LocalMediaAvailability.Available, "C:/Music/first.mp3"));
            await store.UpsertAsync(CreateTrack("Other Artist", "Missing Song", LocalMediaAvailability.Missing, "C:/Music/missing.mp3"));
        }

        await using (var context = new SockseekDbContext(database.Options))
        {
            var store = new LocalLibraryQueryStore(context);

            var availableOnly = await store.SearchAsync(new LocalLibrarySearchRequest("song", IncludeMissing: false));
            Assert.AreEqual(1, availableOnly.TotalCount);
            Assert.AreEqual(availableId, availableOnly.Items.Single().TrackId);
            Assert.AreEqual(1, availableOnly.Items.Single().AvailableFileCount);
            Assert.AreEqual(0, availableOnly.Items.Single().MissingFileCount);

            var withMissing = await store.SearchAsync(new LocalLibrarySearchRequest("song", IncludeMissing: true, Limit: 1));
            Assert.AreEqual(2, withMissing.TotalCount);
            Assert.AreEqual(1, withMissing.Items.Count);
        }
    }

    [TestMethod]
    public async Task SearchAsync_TenThousandTrackFixture_ReturnsFirstPageQuickly()
    {
        await using var database = await TestDatabase.CreateAsync();

        await using (var context = new SockseekDbContext(database.Options))
        {
            for (int i = 0; i < 10_000; i++)
            {
                var track = new CanonicalTrackEntity
                {
                    Id = Guid.NewGuid(),
                    Artist = $"Artist {i % 100:D3}",
                    Title = $"Track {i:D5}",
                    DurationMs = 180000 + i,
                    NormalizedArtist = NormalizeForMatch($"Artist {i % 100:D3}"),
                    NormalizedTitle = NormalizeForMatch($"Track {i:D5}"),
                };
                track.LocalMediaFiles.Add(new LocalMediaFileEntity
                {
                    Id = Guid.NewGuid(),
                    Path = $"C:/Music/Artist {i % 100:D3}/Track {i:D5}.mp3",
                    Size = 1024 + i,
                    LastWriteUtc = new DateTimeOffset(2026, 9, 17, 21, 0, 0, TimeSpan.Zero),
                    DurationMs = track.DurationMs,
                    Codec = "mp3",
                    Bitrate = 320,
                    SampleRate = 44100,
                    BitDepth = 16,
                    Availability = (int)LocalMediaAvailability.Available,
                });
                context.CanonicalTracks.Add(track);

                if (i % 500 == 499)
                    await context.SaveChangesAsync();
            }

            await context.SaveChangesAsync();
        }

        await using (var context = new SockseekDbContext(database.Options))
        {
            var store = new LocalLibraryQueryStore(context);
            var stopwatch = Stopwatch.StartNew();

            var result = await store.SearchAsync(new LocalLibrarySearchRequest("artist 042", Limit: 50));

            stopwatch.Stop();
            Assert.AreEqual(100, result.TotalCount);
            Assert.AreEqual(50, result.Items.Count);
            Assert.IsTrue(
                stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                $"Expected 10k search first page under 2 seconds, actual {stopwatch.Elapsed}.");
            Assert.IsTrue(result.Items.All(item => item.AvailableFileCount == 1));
        }
    }

    [TestMethod]
    public async Task SearchAsync_InvalidPaging_Throws()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = new SockseekDbContext(database.Options);
        var store = new LocalLibraryQueryStore(context);

        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() =>
            store.SearchAsync(new LocalLibrarySearchRequest(Offset: -1)));
        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() =>
            store.SearchAsync(new LocalLibrarySearchRequest(Limit: 0)));
        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() =>
            store.SearchAsync(new LocalLibrarySearchRequest(Limit: 501)));
    }

    private static CanonicalTrackRecord CreateTrack(
        string artist,
        string title,
        LocalMediaAvailability availability,
        string path)
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

    private static string NormalizeForMatch(string value)
    {
        var chars = value.Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();

        return string.Join(' ', new string(chars)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
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
}
