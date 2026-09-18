using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public class PlaylistLocalMatchResolverTests
{
    [TestMethod]
    public async Task ResolveAsync_ExactLocalMatch_MarksPlaylistItemAvailableLocal()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid playlistId;
        Guid trackId;

        await using (var context = new SockseekDbContext(database.Options))
        {
            playlistId = await new ExternalPlaylistSnapshotStore(context).UpsertAsync(CreateSnapshot("item-1", "Artist", "Track", 180000));
            trackId = await new CanonicalTrackStore(context).UpsertAsync(CreateTrack("Artist", "Track", 180000, LocalMediaAvailability.Available));

            var resolver = new PlaylistLocalMatchResolver(context, new TrackIdentityService());
            var result = await resolver.ResolveAsync(playlistId);

            Assert.AreEqual(1, result.MatchedItems);
            Assert.AreEqual(0, result.UnresolvedItems);
        }

        await using var verify = new SockseekDbContext(database.Options);
        var item = await verify.PlaylistItems.SingleAsync();
        Assert.AreEqual(trackId, item.CanonicalTrackId);
        Assert.AreEqual((int)PlaylistItemStatus.AvailableLocal, item.Status);
    }

    [TestMethod]
    public async Task ResolveAsync_OnlyMissingLocalFile_LeavesPlaylistItemUnresolved()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid playlistId;

        await using (var context = new SockseekDbContext(database.Options))
        {
            playlistId = await new ExternalPlaylistSnapshotStore(context).UpsertAsync(CreateSnapshot("item-1", "Artist", "Track", 180000));
            await new CanonicalTrackStore(context).UpsertAsync(CreateTrack("Artist", "Track", 180000, LocalMediaAvailability.Missing));

            var resolver = new PlaylistLocalMatchResolver(context, new TrackIdentityService());
            var result = await resolver.ResolveAsync(playlistId);

            Assert.AreEqual(0, result.MatchedItems);
            Assert.AreEqual(1, result.UnresolvedItems);
        }

        await using var verify = new SockseekDbContext(database.Options);
        var item = await verify.PlaylistItems.SingleAsync();
        Assert.IsNull(item.CanonicalTrackId);
        Assert.AreEqual((int)PlaylistItemStatus.Unresolved, item.Status);
    }

    private static ExternalPlaylistSnapshotRecord CreateSnapshot(string providerItemId, string artist, string title, int? durationMs)
        => new(
            ExternalProvider.Spotify,
            "playlist-1",
            "Daily Mix",
            "https://example.test/playlist/1",
            1,
            new DateTimeOffset(2026, 9, 17, 20, 0, 0, TimeSpan.Zero),
            PlaylistImportMode.Copy,
            "Daily Mix",
            [new ExternalPlaylistItemSnapshot(providerItemId, 1, title, artist, "Album", durationMs)],
            null);

    private static CanonicalTrackRecord CreateTrack(
        string artist,
        string title,
        int? durationMs,
        LocalMediaAvailability availability)
        => new(
            artist,
            title,
            null,
            durationMs,
            null,
            null,
            [],
            [new LocalMediaFileRecord(
                $"C:/Music/{artist}/{title}.mp3",
                1234,
                new DateTimeOffset(2026, 9, 17, 20, 1, 0, TimeSpan.Zero),
                durationMs,
                "mp3",
                320,
                44100,
                16,
                availability)]);

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
