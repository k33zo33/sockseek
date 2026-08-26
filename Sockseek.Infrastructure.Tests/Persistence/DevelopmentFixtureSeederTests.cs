using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public class DevelopmentFixtureSeederTests
{
    [TestMethod]
    public async Task SeedAsync_NonDevelopment_DoesNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new SockseekDbContext(options);
        await context.Database.MigrateAsync();

        var seeder = new DevelopmentFixtureSeeder(context);
        var result = await seeder.SeedAsync(isDevelopment: false);

        Assert.IsFalse(result.Applied);
        Assert.AreEqual(0, result.EntityCount);
        Assert.AreEqual(0, await context.AppProfiles.CountAsync());
        Assert.AreEqual(0, await context.Playlists.CountAsync());
    }

    [TestMethod]
    public async Task SeedAsync_Development_SeedsFixtureGraphOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new SockseekDbContext(options))
            await setup.Database.MigrateAsync();

        DevelopmentSeedResult firstResult;
        DevelopmentSeedResult secondResult;
        await using (var firstContext = new SockseekDbContext(options))
        {
            var seeder = new DevelopmentFixtureSeeder(firstContext);
            firstResult = await seeder.SeedAsync(isDevelopment: true);
            secondResult = await seeder.SeedAsync(isDevelopment: true);
        }

        Assert.IsTrue(firstResult.Applied);
        Assert.AreEqual(10, firstResult.EntityCount);
        Assert.IsFalse(secondResult.Applied);

        await using var verify = new SockseekDbContext(options);
        Assert.AreEqual(1, await verify.AppProfiles.CountAsync());
        Assert.AreEqual(1, await verify.ExternalAccounts.CountAsync());
        Assert.AreEqual(1, await verify.ExternalPlaylists.CountAsync());
        Assert.AreEqual(1, await verify.Playlists.CountAsync());
        Assert.AreEqual(1, await verify.PlaylistItems.CountAsync());
        Assert.AreEqual(1, await verify.CanonicalTracks.CountAsync());
        Assert.AreEqual(1, await verify.TrackSources.CountAsync());
        Assert.AreEqual(1, await verify.LocalMediaFiles.CountAsync());
        Assert.AreEqual(1, await verify.AppSettings.CountAsync());
        Assert.AreEqual(1, await verify.SchemaInfos.CountAsync());

        var account = await verify.ExternalAccounts.SingleAsync();
        Assert.AreEqual("secret://fixtures/spotify/main", account.SecretReference);

        var playlist = await verify.Playlists.SingleAsync();
        Assert.IsNotNull(playlist.ExternalPlaylistId);
    }
}
