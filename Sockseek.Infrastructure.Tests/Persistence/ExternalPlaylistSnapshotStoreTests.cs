using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Domain.Workflows;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;
using Sockseek.Integrations.Abstractions;

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
    public async Task UpsertAsync_RepeatedMirrorSnapshot_PreservesResolvedLocalStatus()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new SockseekDbContext(options))
            await setup.Database.EnsureCreatedAsync();

        var snapshot = CreateSnapshot();
        var trackId = Guid.NewGuid();

        await using (var context = new SockseekDbContext(options))
        {
            var store = new ExternalPlaylistSnapshotStore(context);
            await store.UpsertAsync(snapshot);

            context.CanonicalTracks.Add(new CanonicalTrackEntity
            {
                Id = trackId,
                Artist = "Artist",
                Title = "Track One",
                AlbumTitle = "Album",
                DurationMs = 180000,
                NormalizedArtist = "artist",
                NormalizedTitle = "track one",
            });

            var item = await context.PlaylistItems.SingleAsync(entity => entity.ProviderItemId == "item-1");
            item.CanonicalTrackId = trackId;
            item.Status = (int)PlaylistItemStatus.AvailableLocal;
            await context.SaveChangesAsync();
        }

        await using (var context = new SockseekDbContext(options))
        {
            var store = new ExternalPlaylistSnapshotStore(context);
            await store.UpsertAsync(snapshot with { LastSyncedAtUtc = snapshot.LastSyncedAtUtc.AddMinutes(5) });
        }

        await using (var verify = new SockseekDbContext(options))
        {
            var item = await verify.PlaylistItems.SingleAsync(entity => entity.ProviderItemId == "item-1");

            Assert.AreEqual(trackId, item.CanonicalTrackId);
            Assert.AreEqual((int)PlaylistItemStatus.AvailableLocal, item.Status);
            Assert.IsNull(item.RemovedAtUtc);
            Assert.AreEqual(2, await verify.PlaylistItems.CountAsync());
        }
    }

    [TestMethod]
    public async Task UpsertAsync_RepeatedMirrorSnapshot_PreservesManualReviewDecisions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new SockseekDbContext(options))
            await setup.Database.EnsureCreatedAsync();

        var snapshot = CreateSnapshot();
        var trackId = Guid.NewGuid();
        Guid approvedItemId;
        Guid rejectedItemId;

        await using (var context = new SockseekDbContext(options))
        {
            var store = new ExternalPlaylistSnapshotStore(context);
            await store.UpsertAsync(snapshot);

            context.CanonicalTracks.Add(new CanonicalTrackEntity
            {
                Id = trackId,
                Artist = "Artist",
                Title = "Track One",
                AlbumTitle = "Album",
                DurationMs = 180000,
                NormalizedArtist = "artist",
                NormalizedTitle = "track one",
            });

            var approved = await context.PlaylistItems.SingleAsync(entity => entity.ProviderItemId == "item-1");
            approvedItemId = approved.Id;
            approved.CanonicalTrackId = trackId;
            approved.Status = (int)PlaylistItemStatus.AvailableLocal;
            context.ResolutionAttempts.Add(new ResolutionAttemptEntity
            {
                Id = Guid.NewGuid(),
                PlaylistItemId = approved.Id,
                CandidateTrackId = trackId,
                Method = (int)ResolutionMethod.ManualReview,
                Score = 1d,
                Decision = (int)ResolutionDecision.UserApproved,
                CreatedAtUtc = snapshot.LastSyncedAtUtc.AddMinutes(1),
            });

            var rejected = await context.PlaylistItems.SingleAsync(entity => entity.ProviderItemId == "item-2");
            rejectedItemId = rejected.Id;
            rejected.CanonicalTrackId = null;
            rejected.Status = (int)PlaylistItemStatus.Unresolved;
            context.ResolutionAttempts.Add(new ResolutionAttemptEntity
            {
                Id = Guid.NewGuid(),
                PlaylistItemId = rejected.Id,
                CandidateTrackId = trackId,
                Method = (int)ResolutionMethod.ManualReview,
                Score = 0d,
                Decision = (int)ResolutionDecision.UserRejected,
                CreatedAtUtc = snapshot.LastSyncedAtUtc.AddMinutes(2),
            });

            await context.SaveChangesAsync();
        }

        var synced = snapshot with
        {
            LastSyncedAtUtc = snapshot.LastSyncedAtUtc.AddMinutes(10),
            SnapshotVersion = snapshot.SnapshotVersion + 1,
            Items = new[]
            {
                new ExternalPlaylistItemSnapshot("item-1", 2, "Track One Updated", "Artist", "Album", 180000),
                new ExternalPlaylistItemSnapshot("item-2", 1, "Track Two Updated", "Artist", "Album", 181000),
            },
        };

        await using (var context = new SockseekDbContext(options))
        {
            var store = new ExternalPlaylistSnapshotStore(context);
            await store.UpsertAsync(synced);
        }

        await using (var verify = new SockseekDbContext(options))
        {
            var approved = await verify.PlaylistItems.SingleAsync(entity => entity.Id == approvedItemId);
            var rejected = await verify.PlaylistItems.SingleAsync(entity => entity.Id == rejectedItemId);
            var attempts = await verify.ResolutionAttempts
                .AsNoTracking()
                .Where(attempt => attempt.PlaylistItemId == approvedItemId || attempt.PlaylistItemId == rejectedItemId)
                .ToListAsync();

            Assert.AreEqual(trackId, approved.CanonicalTrackId);
            Assert.AreEqual((int)PlaylistItemStatus.AvailableLocal, approved.Status);
            Assert.AreEqual(2, approved.Position);
            StringAssert.Contains(approved.SnapshotJson, "Track One Updated");

            Assert.IsNull(rejected.CanonicalTrackId);
            Assert.AreEqual((int)PlaylistItemStatus.Unresolved, rejected.Status);
            Assert.AreEqual(1, rejected.Position);
            StringAssert.Contains(rejected.SnapshotJson, "Track Two Updated");

            Assert.AreEqual(2, attempts.Count);
            Assert.AreEqual((int)ResolutionDecision.UserApproved, attempts.Single(attempt => attempt.PlaylistItemId == approvedItemId).Decision);
            Assert.AreEqual((int)ResolutionDecision.UserRejected, attempts.Single(attempt => attempt.PlaylistItemId == rejectedItemId).Decision);
        }
    }

    [TestMethod]
    public async Task UpsertAsync_ProviderSnapshot_PreservesSpotifyMetadataInSnapshotJson()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new SockseekDbContext(options))
            await setup.Database.EnsureCreatedAsync();

        var providerSnapshot = new ExternalPlaylistSnapshot(
            ProviderIds.Spotify,
            "spotify-playlist-1",
            "Spotify Mix",
            "https://open.spotify.com/playlist/spotify-playlist-1",
            42,
            new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero),
            [
                new ExternalTrackSnapshot(
                    ProviderIds.Spotify,
                    "spotify-track-1",
                    "spotify:track:spotify-track-1",
                    0,
                    "Spotify Track",
                    ["Artist One", "Artist Two"],
                    "Spotify Album",
                    194000,
                    "USRC17607839",
                    "https://open.spotify.com/track/spotify-track-1",
                    "https://i.scdn.co/image/spotify-track-1",
                    "mbid-1",
                    "{\"spotify\":\"raw\"}"),
            ]);
        var account = new ExternalAccountSnapshot(
            new ExternalAccountId(Guid.NewGuid()),
            ProviderIds.Spotify,
            "spotify-user-1",
            "Spotify User",
            "secret://spotify/1",
            new DateTimeOffset(2026, 9, 23, 11, 55, 0, TimeSpan.Zero));

        var record = ExternalPlaylistSnapshotRecordFactory.FromProviderSnapshot(
            providerSnapshot,
            PlaylistImportMode.Copy,
            account);

        await using (var context = new SockseekDbContext(options))
            await new ExternalPlaylistSnapshotStore(context).UpsertAsync(record);

        await using (var verify = new SockseekDbContext(options))
        {
            var playlistItem = await verify.PlaylistItems.SingleAsync();

            StringAssert.Contains(playlistItem.SnapshotJson, "\"ExternalTrackId\":\"spotify-track-1\"");
            StringAssert.Contains(playlistItem.SnapshotJson, "\"Isrc\":\"USRC17607839\"");
            StringAssert.Contains(playlistItem.SnapshotJson, "\"ExternalUrl\":\"https://open.spotify.com/track/spotify-track-1\"");
            StringAssert.Contains(playlistItem.SnapshotJson, "\"ArtworkUrl\":\"https://i.scdn.co/image/spotify-track-1\"");
            StringAssert.Contains(playlistItem.SnapshotJson, "\"MusicBrainzRecordingId\":\"mbid-1\"");
            StringAssert.Contains(playlistItem.SnapshotJson, "\"RawMetadataJson\":\"{\\u0022spotify\\u0022:\\u0022raw\\u0022}\"");
            Assert.AreEqual(0, playlistItem.Position);
            Assert.AreEqual((int)PlaylistImportMode.Copy, await verify.Playlists.Select(playlist => playlist.ImportMode).SingleAsync());
            Assert.AreEqual("spotify-user-1", await verify.ExternalAccounts.Select(account => account.ExternalUserId).SingleAsync());
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

            await using (var context = new SockseekDbContext(options))
            {
                await context.Database.MigrateAsync();

                Assert.IsTrue(await context.Database.CanConnectAsync());
                Assert.IsTrue(await context.ExternalPlaylists.AnyAsync() == false);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
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
