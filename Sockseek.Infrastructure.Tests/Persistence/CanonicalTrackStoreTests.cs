using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public class CanonicalTrackStoreTests
{
    [TestMethod]
    public async Task UpsertAsync_RepeatedRecord_DoesNotDuplicateTrackSourcesOrLocalMediaFiles()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new SockseekDbContext(options))
            await setup.Database.MigrateAsync();

        var record = new CanonicalTrackRecord(
            "Artist",
            "Track",
            180000,
            "HR-ABC-01",
            null,
            [new TrackSourceRecord(ExternalProvider.Spotify, "spotify:track:1", "https://example.test/track/1", "{\"provider\":\"spotify\"}")],
            [new LocalMediaFileRecord("C:/Music/Artist/Track.mp3", 1024, new DateTimeOffset(2026, 8, 4, 20, 0, 0, TimeSpan.Zero), 180000, "mp3", 320, 44100, 16, LocalMediaAvailability.Available)]);

        Guid firstId;
        Guid secondId;
        await using (var context = new SockseekDbContext(options))
        {
            var store = new CanonicalTrackStore(context);
            firstId = await store.UpsertAsync(record);
            secondId = await store.UpsertAsync(record with
            {
                LocalMediaFiles = [new LocalMediaFileRecord("C:\\Music\\Artist\\Track.mp3", 2048, new DateTimeOffset(2026, 8, 4, 20, 5, 0, TimeSpan.Zero), 180000, "mp3", 320, 44100, 16, LocalMediaAvailability.Available)]
            });
        }

        Assert.AreEqual(firstId, secondId);

        await using (var verify = new SockseekDbContext(options))
        {
            Assert.AreEqual(1, await verify.CanonicalTracks.CountAsync());
            Assert.AreEqual(1, await verify.TrackSources.CountAsync());
            Assert.AreEqual(1, await verify.LocalMediaFiles.CountAsync());

            var file = await verify.LocalMediaFiles.SingleAsync();
            Assert.AreEqual(2048, file.Size);
            Assert.AreEqual("C:/Music/Artist/Track.mp3", file.Path);
        }
    }

    [TestMethod]
    public async Task UpsertAsync_ExistingPathFromOtherTrack_ReassignsLocalMediaWithoutDuplication()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new SockseekDbContext(options))
            await setup.Database.MigrateAsync();

        await using (var context = new SockseekDbContext(options))
        {
            var store = new CanonicalTrackStore(context);
            await store.UpsertAsync(new CanonicalTrackRecord(
                "Artist A",
                "Track A",
                100000,
                null,
                null,
                [],
                [new LocalMediaFileRecord("/music/shared.mp3", 111, DateTimeOffset.UtcNow, 100000, "mp3", 320, 44100, 16, LocalMediaAvailability.Available)]));

            await store.UpsertAsync(new CanonicalTrackRecord(
                "Artist B",
                "Track B",
                120000,
                null,
                "mbid-b",
                [],
                [new LocalMediaFileRecord("/music/shared.mp3", 222, DateTimeOffset.UtcNow, 120000, "mp3", 320, 44100, 16, LocalMediaAvailability.Available)]));
        }

        await using (var verify = new SockseekDbContext(options))
        {
            Assert.AreEqual(2, await verify.CanonicalTracks.CountAsync());
            Assert.AreEqual(1, await verify.LocalMediaFiles.CountAsync());
            var file = await verify.LocalMediaFiles.SingleAsync();
            Assert.AreEqual(222, file.Size);
            Assert.IsNotNull(file.CanonicalTrackId);
        }
    }
}
