using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public class ExternalAccountStoreTests
{
    [TestMethod]
    public async Task DeleteAsync_RemovesAccount_WithoutDeletingCanonicalTracksOrLocalMediaFiles()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        Guid accountId;
        Guid playlistId;
        await using (var setup = new SockseekDbContext(options))
        {
            await setup.Database.MigrateAsync();

            var track = new CanonicalTrackEntity
            {
                Id = Guid.NewGuid(),
                Artist = "Artist",
                Title = "Track",
                DurationMs = 180000,
                NormalizedArtist = "artist",
                NormalizedTitle = "track",
            };
            var media = new LocalMediaFileEntity
            {
                Id = Guid.NewGuid(),
                CanonicalTrack = track,
                Path = "/music/artist/track.mp3",
                Size = 100,
                LastWriteUtc = DateTimeOffset.UtcNow,
                DurationMs = 180000,
                Availability = 0,
            };
            var account = new ExternalAccountEntity
            {
                Id = Guid.NewGuid(),
                Provider = 0,
                ExternalUserId = "user-1",
                DisplayName = "Alice",
                SecretReference = "secret://spotify/1",
                Status = 1,
            };
            var externalPlaylist = new ExternalPlaylistEntity
            {
                Id = Guid.NewGuid(),
                Account = account,
                Provider = 0,
                ExternalId = "playlist-1",
                Name = "Daily Mix",
                SnapshotVersion = 1,
                LastSyncedAtUtc = DateTimeOffset.UtcNow,
            };
            var playlist = new PlaylistEntity
            {
                Id = Guid.NewGuid(),
                Name = "Daily Mix",
                ImportMode = 1,
                ExternalPlaylist = externalPlaylist,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Items =
                [
                    new PlaylistItemEntity
                    {
                        Id = Guid.NewGuid(),
                        ProviderItemId = "item-1",
                        Position = 1,
                        Status = 0,
                        SnapshotJson = "{}",
                        CanonicalTrack = track,
                    },
                ],
            };

            setup.AddRange(track, media, account, externalPlaylist, playlist);
            await setup.SaveChangesAsync();
            accountId = account.Id;
            playlistId = externalPlaylist.Id;
        }

        await using (var context = new SockseekDbContext(options))
        {
            var store = new ExternalAccountStore(context);
            Assert.IsTrue(await store.DeleteAsync(accountId));
        }

        await using (var verify = new SockseekDbContext(options))
        {
            Assert.AreEqual(0, await verify.ExternalAccounts.CountAsync());
            Assert.AreEqual(1, await verify.ExternalPlaylists.CountAsync());
            Assert.AreEqual(1, await verify.CanonicalTracks.CountAsync());
            Assert.AreEqual(1, await verify.LocalMediaFiles.CountAsync());

            var playlist = await verify.ExternalPlaylists.SingleAsync(x => x.Id == playlistId);
            Assert.IsNull(playlist.AccountId);
        }
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsFalseWhenAccountDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new SockseekDbContext(options))
            await setup.Database.MigrateAsync();

        await using var context = new SockseekDbContext(options);
        var store = new ExternalAccountStore(context);
        Assert.IsFalse(await store.DeleteAsync(Guid.NewGuid()));
    }
}
