using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Common;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public class LibraryRootStoreTests
{
    [TestMethod]
    public async Task AddOrUpdateAsync_RepeatedPath_UpdatesExistingRootWithoutDuplication()
    {
        await using var database = await TestDatabase.CreateAsync();
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 17, 18, 0, 0, TimeSpan.Zero));
        string root = Path.Combine(Path.GetTempPath(), "sockseek-library-root");

        Guid firstId;
        Guid secondId;
        await using (var context = new SockseekDbContext(database.Options))
        {
            var store = new LibraryRootStore(context, clock);
            firstId = await store.AddOrUpdateAsync(root, "Music");
            clock.UtcNow = clock.UtcNow.AddMinutes(5);
            secondId = await store.AddOrUpdateAsync(root, "Archive", enabled: false);
        }

        Assert.AreEqual(firstId, secondId);

        await using var verify = new SockseekDbContext(database.Options);
        var entity = await verify.LibraryRoots.SingleAsync();
        Assert.AreEqual(NormalizeDirectoryPath(root), entity.Path);
        Assert.AreEqual("Archive", entity.DisplayName);
        Assert.IsFalse(entity.Enabled);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 17, 18, 0, 0, TimeSpan.Zero), entity.CreatedAtUtc);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 17, 18, 5, 0, TimeSpan.Zero), entity.UpdatedAtUtc);
    }

    [TestMethod]
    public async Task ScanMarkers_UpdateRootTimestamps()
    {
        await using var database = await TestDatabase.CreateAsync();
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 17, 19, 0, 0, TimeSpan.Zero));
        Guid id;

        await using (var context = new SockseekDbContext(database.Options))
        {
            var store = new LibraryRootStore(context, clock);
            id = await store.AddOrUpdateAsync(Path.GetTempPath());
            await store.MarkScanStartedAsync(id);
            clock.UtcNow = clock.UtcNow.AddMinutes(2);
            await store.MarkScanCompletedAsync(id);
        }

        await using var verify = new SockseekDbContext(database.Options);
        var entity = await verify.LibraryRoots.SingleAsync();
        Assert.AreEqual(new DateTimeOffset(2026, 9, 17, 19, 0, 0, TimeSpan.Zero), entity.LastScanStartedUtc);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 17, 19, 2, 0, TimeSpan.Zero), entity.LastScanCompletedUtc);
        Assert.AreEqual(entity.LastScanCompletedUtc, entity.UpdatedAtUtc);
    }

    [TestMethod]
    public async Task RemoveAsync_ExistingRoot_RemovesOnlyRootRecord()
    {
        await using var database = await TestDatabase.CreateAsync();
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        Guid id;

        await using (var context = new SockseekDbContext(database.Options))
        {
            var store = new LibraryRootStore(context, clock);
            id = await store.AddOrUpdateAsync(Path.GetTempPath());
            Assert.IsTrue(await store.RemoveAsync(id));
            Assert.IsFalse(await store.RemoveAsync(id));
        }

        await using var verify = new SockseekDbContext(database.Options);
        Assert.AreEqual(0, await verify.LibraryRoots.CountAsync());
    }

    private static string NormalizeDirectoryPath(string path)
    {
        string normalized = Path.GetFullPath(path).Trim().Replace('\\', '/').TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized + "/";
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

        public ValueTask DisposeAsync()
            => connection.DisposeAsync();
    }
}
