using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public class ExternalPlaylistSnapshotStoreTests
{
    [TestMethod]
    public async Task UpsertAsync_RepeatedSnapshot_DoesNotDuplicatePlaylistOrItems()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new SockseekDbContext(options))
            await setup.Database.EnsureCreatedAsync();

        var snapshot = CreateSnapshot();

        await using (var context = new SockseekDbContext(options))
        {
            var store = new ExternalPlaylistSnapshotStore(context);
            await store.UpsertAsync(snapshot);
            await store.UpsertAsync(snapshot with { LastSyncedAtUtc = snapshot.LastSyncedAtUtc.AddMinutes(5) });
        }

        await using (var verify = new SockseekDbContext(options))
        {
            Assert.AreEqual(1, await verify.ExternalAccounts.CountAsync());
            Assert.AreEqual(1, await verify.ExternalPlaylists.CountAsync());
            Assert.AreEqual(1, await verify.Playlists.CountAsync());
            Assert.AreEqual(2, await verify.PlaylistItems.CountAsync());

            var itemIds = await verify.PlaylistItems
                .OrderBy(item => item.Position)
                .Select(item => item.ProviderItemId)
                .ToListAsync();
            CollectionAssert.AreEqual(new[] { "item-1", "item-2" }, itemIds);
        }
    }

    [TestMethod]
    public async Task UpsertAsync_MirrorImport_MarksMissingItemsRemoved_AndReusesPlaylist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new SockseekDbContext(options))
            await setup.Database.EnsureCreatedAsync();

        var first = CreateSnapshot();
        var second = first with
        {
            Items = new[]
            {
                new ExternalPlaylistItemSnapshot("item-1", 1, "Track One", "Artist", "Album", 180000),
            },
            LastSyncedAtUtc = first.LastSyncedAtUtc.AddMinutes(10),
            SnapshotVersion = first.SnapshotVersion + 1,
        };

        Guid playlistId;
        await using (var context = new SockseekDbContext(options))
        {
            var store = new ExternalPlaylistSnapshotStore(context);
            playlistId = await store.UpsertAsync(first);
            var secondPlaylistId = await store.UpsertAsync(second);
            Assert.AreEqual(playlistId, secondPlaylistId);
        }

        await using (var verify = new SockseekDbContext(options))
        {
            var removed = await verify.PlaylistItems.SingleAsync(item => item.ProviderItemId == "item-2");
            Assert.AreEqual(9, removed.Status);
            Assert.AreEqual(second.LastSyncedAtUtc, removed.RemovedAtUtc);
        }
    }

    [TestMethod]
    public async Task MigrateAsync_CreatesSchemaFromEmptyDatabase()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"sockseek-migrate-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<SockseekDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            await using var context = new SockseekDbContext(options);
            await context.Database.MigrateAsync();

            Assert.IsTrue(await context.Database.CanConnectAsync());
            Assert.IsTrue(await context.ExternalPlaylists.AnyAsync() == false);
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    private static ExternalPlaylistSnapshotRecord CreateSnapshot()
        => new(
            ExternalProvider.Spotify,
            "playlist-1",
            "Daily Mix",
            "https://example.test/playlist/1",
            1,
            new DateTimeOffset(2026, 8, 4, 20, 0, 0, TimeSpan.Zero),
            PlaylistImportMode.Mirror,
            "Daily Mix",
            new[]
            {
                new ExternalPlaylistItemSnapshot("item-1", 1, "Track One", "Artist", "Album", 180000),
                new ExternalPlaylistItemSnapshot("item-2", 2, "Track Two", "Artist", "Album", 181000),
            },
            new ExternalAccountRecord(
                ExternalProvider.Spotify,
                "user-1",
                "Alice",
                "secret://spotify/1",
                new DateTimeOffset(2026, 8, 4, 19, 55, 0, TimeSpan.Zero)));
}
