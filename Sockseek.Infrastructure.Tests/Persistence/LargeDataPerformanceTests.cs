using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class LargeDataPerformanceTests
{
    private const string RunLargeDataEnvVar = "SOCKSEEK_RUN_LARGE_DATA";
    private const int LibraryTrackCount = 100_000;
    private const int PlaylistItemCount = 10_000;
    private static readonly TimeSpan LibrarySearchBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PlaylistDetailBudget = TimeSpan.FromSeconds(10);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Performance")]
    [Timeout(180_000)]
    public async Task SearchAsync_HundredThousandTrackFixture_ReturnsFirstPageWithinBudget()
    {
        if (!ShouldRunLargeData())
        {
            TestContext.WriteLine(
                $"Skipped opt-in large-data performance test. Set {RunLargeDataEnvVar}=1 to run it.");
            return;
        }

        await using var database = await TestDatabase.CreateAsync();
        await SeedLibraryAsync(database.Options, LibraryTrackCount);

        await using var context = new SockseekDbContext(database.Options);
        var store = new LocalLibraryQueryStore(context);
        var stopwatch = Stopwatch.StartNew();

        var result = await store.SearchAsync(new LocalLibrarySearchRequest("artist 0042", Limit: 100));

        stopwatch.Stop();
        TestContext.WriteLine($"100k library search elapsed: {stopwatch.Elapsed}.");
        Assert.AreEqual(100, result.TotalCount);
        Assert.AreEqual(100, result.Items.Count);
        Assert.IsTrue(
            stopwatch.Elapsed < LibrarySearchBudget,
            $"Expected 100k library first-page search under {LibrarySearchBudget}, actual {stopwatch.Elapsed}.");
    }

    [TestMethod]
    [TestCategory("Performance")]
    [Timeout(180_000)]
    public async Task GetDetailAsync_TenThousandItemPlaylist_ReturnsDetailWithinBudget()
    {
        if (!ShouldRunLargeData())
        {
            TestContext.WriteLine(
                $"Skipped opt-in large-data performance test. Set {RunLargeDataEnvVar}=1 to run it.");
            return;
        }

        await using var database = await TestDatabase.CreateAsync();
        Guid playlistId;
        await using (var context = new SockseekDbContext(database.Options))
        {
            playlistId = await new ExternalPlaylistSnapshotStore(context).UpsertAsync(CreateLargePlaylistSnapshot());
        }

        await using var verify = new SockseekDbContext(database.Options);
        var store = new PlaylistQueryStore(verify);
        var stopwatch = Stopwatch.StartNew();

        var detail = await store.GetDetailAsync(playlistId);

        stopwatch.Stop();
        TestContext.WriteLine($"10k playlist detail elapsed: {stopwatch.Elapsed}.");
        Assert.IsNotNull(detail);
        Assert.AreEqual(PlaylistItemCount, detail.Items.Count);
        Assert.AreEqual(PlaylistItemCount, detail.Resolution.TotalItems);
        Assert.IsTrue(
            stopwatch.Elapsed < PlaylistDetailBudget,
            $"Expected 10k playlist detail under {PlaylistDetailBudget}, actual {stopwatch.Elapsed}.");
    }

    private static async Task SeedLibraryAsync(DbContextOptions<SockseekDbContext> options, int count)
    {
        await using var context = new SockseekDbContext(options);
        for (int i = 0; i < count; i++)
        {
            var track = new CanonicalTrackEntity
            {
                Id = Guid.NewGuid(),
                Artist = $"Artist {i % 1000:D4}",
                Title = $"Track {i:D6}",
                AlbumTitle = $"Album {i / 12:D5}",
                DurationMs = 180000 + i,
                NormalizedArtist = NormalizeForMatch($"Artist {i % 1000:D4}"),
                NormalizedTitle = NormalizeForMatch($"Track {i:D6}"),
            };
            track.LocalMediaFiles.Add(new LocalMediaFileEntity
            {
                Id = Guid.NewGuid(),
                Path = $"C:/Music/Artist {i % 1000:D4}/Track {i:D6}.mp3",
                Size = 4096 + i,
                LastWriteUtc = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero),
                DurationMs = track.DurationMs,
                Codec = "mp3",
                Bitrate = 320,
                SampleRate = 44100,
                BitDepth = 16,
                Availability = (int)LocalMediaAvailability.Available,
            });
            context.CanonicalTracks.Add(track);

            if (i % 1000 == 999)
                await context.SaveChangesAsync();
        }

        await context.SaveChangesAsync();
    }

    private static ExternalPlaylistSnapshotRecord CreateLargePlaylistSnapshot()
        => new(
            ExternalProvider.Spotify,
            "large-playlist-1",
            "Large Playlist",
            "https://example.test/playlists/large",
            1,
            new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero),
            PlaylistImportMode.Mirror,
            "Large Playlist",
            Enumerable.Range(1, PlaylistItemCount)
                .Select(index => new ExternalPlaylistItemSnapshot(
                    $"item-{index:D5}",
                    index,
                    $"Track {index:D5}",
                    "Load Artist",
                    "Load Album",
                    180000 + index,
                    ExternalTrackId: $"track-{index:D5}",
                    Isrc: null,
                    ExternalUrl: $"https://example.test/tracks/{index:D5}",
                    ArtworkUrl: null,
                    MusicBrainzRecordingId: null,
                    Artists: null))
                .ToArray(),
            new ExternalAccountRecord(
                ExternalProvider.Spotify,
                "large-user",
                "Large User",
                "secret://spotify/large",
                new DateTimeOffset(2026, 10, 6, 11, 55, 0, TimeSpan.Zero)));

    private static string NormalizeForMatch(string value)
    {
        var chars = value.Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();

        return string.Join(' ', new string(chars)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool ShouldRunLargeData()
        => string.Equals(Environment.GetEnvironmentVariable(RunLargeDataEnvVar), "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable(RunLargeDataEnvVar), "true", StringComparison.OrdinalIgnoreCase);

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
