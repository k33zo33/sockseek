using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class PlaylistQueryStoreTests
{
    [TestMethod]
    public async Task GetDetailAsync_ImportedPlaylist_ReturnsItemsAndResolutionSummary()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid playlistId;
        Guid trackId = Guid.NewGuid();
        DateTimeOffset removedAt = new(2026, 9, 27, 12, 5, 0, TimeSpan.Zero);

        await using (var context = new SockseekDbContext(database.Options))
        {
            playlistId = await new ExternalPlaylistSnapshotStore(context).UpsertAsync(CreateSnapshot());
            context.CanonicalTracks.Add(new CanonicalTrackEntity
            {
                Id = trackId,
                Artist = "Artist One",
                Title = "Available Track",
                AlbumTitle = "Album One",
                DurationMs = 180000,
                NormalizedArtist = "artist one",
                NormalizedTitle = "available track",
            });

            var items = await context.PlaylistItems.ToDictionaryAsync(item => item.ProviderItemId);
            items["item-1"].CanonicalTrackId = trackId;
            items["item-1"].Status = (int)PlaylistItemStatus.AvailableLocal;
            items["item-2"].Status = (int)PlaylistItemStatus.Unresolved;
            items["item-3"].Status = (int)PlaylistItemStatus.ReviewRequired;
            items["item-4"].Status = (int)PlaylistItemStatus.Searching;
            items["item-5"].Status = (int)PlaylistItemStatus.CandidateFound;
            items["item-6"].Status = (int)PlaylistItemStatus.Downloading;
            items["item-7"].Status = (int)PlaylistItemStatus.Failed;
            items["item-8"].Status = (int)PlaylistItemStatus.Skipped;
            items["item-9"].Status = (int)PlaylistItemStatus.RemovedFromSourcePlaylist;
            items["item-9"].RemovedAtUtc = removedAt;

            await context.SaveChangesAsync();
        }

        await using var verify = new SockseekDbContext(database.Options);
        var store = new PlaylistQueryStore(verify);

        var summaries = await store.GetSummariesAsync();
        var detail = await store.GetDetailAsync(playlistId);

        Assert.AreEqual(1, summaries.Count);
        Assert.IsNotNull(detail);
        Assert.AreEqual("Daily Mix", detail.Name);
        Assert.AreEqual("Mirror", detail.ImportMode);
        Assert.AreEqual("spotify", detail.ProviderId);
        Assert.AreEqual("playlist-1", detail.ExternalPlaylistId);
        Assert.AreEqual("https://example.test/playlist/1", detail.ExternalUrl);
        Assert.AreEqual(9, detail.Items.Count);
        Assert.AreEqual(9, detail.Resolution.TotalItems);
        Assert.AreEqual(1, detail.Resolution.AvailableLocalItems);
        Assert.AreEqual(1, detail.Resolution.UnresolvedItems);
        Assert.AreEqual(1, detail.Resolution.ReviewRequiredItems);
        Assert.AreEqual(1, detail.Resolution.SearchingItems);
        Assert.AreEqual(1, detail.Resolution.CandidateFoundItems);
        Assert.AreEqual(1, detail.Resolution.DownloadingItems);
        Assert.AreEqual(1, detail.Resolution.FailedItems);
        Assert.AreEqual(1, detail.Resolution.SkippedItems);
        Assert.AreEqual(1, detail.Resolution.RemovedItems);

        var first = detail.Items[0];
        Assert.AreEqual(trackId, first.CanonicalTrackId);
        Assert.AreEqual("AvailableLocal", first.Status);
        Assert.AreEqual("Available Track", first.Title);
        CollectionAssert.AreEqual(new[] { "Artist One", "Guest Artist" }, first.Artists.ToArray());
        Assert.AreEqual("USRC17607839", first.Isrc);
        Assert.AreEqual("mbid-1", first.MusicBrainzRecordingId);
        Assert.AreEqual("https://example.test/track/1", first.ExternalUrl);
        Assert.AreEqual("https://example.test/art/1.jpg", first.ArtworkUrl);
        Assert.AreEqual("RemovedFromSourcePlaylist", detail.Items[^1].Status);
        Assert.AreEqual(removedAt, detail.Items[^1].RemovedAtUtc);
        Assert.AreEqual(detail.Resolution, summaries.Single().Resolution);
    }

    [TestMethod]
    public async Task GetDetailAsync_MissingPlaylist_ReturnsNull()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = new SockseekDbContext(database.Options);

        var detail = await new PlaylistQueryStore(context).GetDetailAsync(Guid.NewGuid());

        Assert.IsNull(detail);
    }

    private static ExternalPlaylistSnapshotRecord CreateSnapshot()
        => new(
            ExternalProvider.Spotify,
            "playlist-1",
            "Daily Mix",
            "https://example.test/playlist/1",
            1,
            new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero),
            PlaylistImportMode.Mirror,
            "Daily Mix",
            Enumerable.Range(1, 9)
                .Select(index => new ExternalPlaylistItemSnapshot(
                    $"item-{index}",
                    index,
                    index == 1 ? "Available Track" : $"Track {index}",
                    "Artist One",
                    "Album One",
                    180000 + index,
                    ExternalTrackId: $"track-{index}",
                    Isrc: index == 1 ? "USRC17607839" : null,
                    ExternalUrl: index == 1 ? "https://example.test/track/1" : null,
                    ArtworkUrl: index == 1 ? "https://example.test/art/1.jpg" : null,
                    MusicBrainzRecordingId: index == 1 ? "mbid-1" : null,
                    Artists: index == 1 ? ["Artist One", "Guest Artist"] : null))
                .ToArray(),
            new ExternalAccountRecord(
                ExternalProvider.Spotify,
                "user-1",
                "Alice",
                "secret://spotify/1",
                new DateTimeOffset(2026, 9, 27, 11, 55, 0, TimeSpan.Zero)));

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
