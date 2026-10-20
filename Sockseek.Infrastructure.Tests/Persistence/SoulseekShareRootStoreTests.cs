using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Common;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class SoulseekShareRootStoreTests
{
    [TestMethod]
    public async Task SaveAsync_RepeatedCanonicalPathUpdatesExplicitRootWithoutDuplication()
    {
        await using var database = await TestDatabase.CreateAsync();
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        string rootPath = Path.Combine(Path.GetTempPath(), "sockseek-share-root");

        SoulseekShareRootRecord first;
        SoulseekShareRootRecord updated;
        await using (var context = new SockseekDbContext(database.Options))
        {
            var store = new SoulseekShareRootStore(context, clock);
            first = await store.SaveAsync(null, rootPath, "Music", enabled: true);
            clock.UtcNow = clock.UtcNow.AddMinutes(5);
            updated = await store.SaveAsync(null, rootPath, "Archive", enabled: false);
        }

        Assert.AreEqual(first.Id, updated.Id);
        Assert.AreEqual(Path.GetFullPath(rootPath), updated.Path);
        Assert.AreEqual("Archive", updated.VirtualName);
        Assert.IsFalse(updated.Enabled);
        Assert.AreEqual(first.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.AreEqual(clock.UtcNow, updated.UpdatedAtUtc);

        await using var verify = new SockseekDbContext(database.Options);
        Assert.AreEqual(1, await verify.SoulseekShareRoots.CountAsync());
    }

    [TestMethod]
    public async Task SaveAsync_VirtualNameIsUniqueIgnoringCase()
    {
        await using var database = await TestDatabase.CreateAsync();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        await using var context = new SockseekDbContext(database.Options);
        var store = new SoulseekShareRootStore(context, clock);
        await store.SaveAsync(null, Path.Combine(Path.GetTempPath(), "share-one"), "Music", enabled: true);

        await Assert.ThrowsExceptionAsync<DbUpdateException>(() => store.SaveAsync(
            null,
            Path.Combine(Path.GetTempPath(), "share-two"),
            "music",
            enabled: true));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("../escape")]
    [DataRow("nested/share")]
    [DataRow("nested\\share")]
    public async Task SaveAsync_InvalidVirtualNameIsRejectedBeforePersistence(string virtualName)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = new SockseekDbContext(database.Options);
        var store = new SoulseekShareRootStore(context, new FakeClock(DateTimeOffset.UtcNow));

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.SaveAsync(
            null,
            Path.GetTempPath(),
            virtualName,
            enabled: true));

        Assert.AreEqual(0, await context.SoulseekShareRoots.CountAsync());
    }

    [TestMethod]
    public async Task RemoveAsync_DeletesOnlyTheExplicitShareConfiguration()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = new SockseekDbContext(database.Options);
        var store = new SoulseekShareRootStore(context, new FakeClock(DateTimeOffset.UtcNow));
        var root = await store.SaveAsync(null, Path.GetTempPath(), "Music", enabled: true);

        Assert.IsTrue(await store.RemoveAsync(root.Id));
        Assert.IsFalse(await store.RemoveAsync(root.Id));
        Assert.AreEqual(0, await context.SoulseekShareRoots.CountAsync());
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
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

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
