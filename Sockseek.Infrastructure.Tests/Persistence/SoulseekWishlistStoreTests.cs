using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Common;
using Sockseek.Domain.Soulseek;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class SoulseekWishlistStoreTests
{
    [TestMethod]
    public async Task Lifecycle_PersistsClaimSubmissionAndResultWithoutDuplicateDueWork()
    {
        await using var database = await TestDatabase.CreateAsync();
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 18, 11, 0, 0, TimeSpan.Zero));
        Guid id;

        await using (var context = new SockseekDbContext(database.Options))
        {
            var store = new SoulseekWishlistStore(context, clock);
            var created = await store.CreateAsync("artist title", 30, enabled: true);
            id = created.Id.Value;
            Assert.AreEqual(1, (await store.ListDueAsync(clock.UtcNow, 10)).Count);

            var claimed = await store.TryClaimAsync(id, force: false);
            Assert.IsNotNull(claimed);
            Assert.IsNull(await store.TryClaimAsync(id, force: true));

            var workflowId = Guid.NewGuid();
            var jobId = Guid.NewGuid();
            await store.MarkSubmittedAsync(id, workflowId, jobId);
            Assert.AreEqual(0, (await store.ListDueAsync(clock.UtcNow.AddHours(1), 10)).Count);

            clock.UtcNow = clock.UtcNow.AddMinutes(1);
            await store.MarkSucceededAsync(id, 7);
        }

        await using (var verifyContext = new SockseekDbContext(database.Options))
        {
            var verify = new SoulseekWishlistStore(verifyContext, clock);
            var entry = await verify.FindAsync(id);
            Assert.IsNotNull(entry);
            Assert.AreEqual(WishlistSearchState.Succeeded, entry.SearchState);
            Assert.AreEqual(7, entry.LastResultCount);
            Assert.IsNull(entry.ActiveJobId);
            Assert.AreEqual(clock.UtcNow.AddMinutes(29), entry.NextSearchAtUtc);
        }
    }

    [TestMethod]
    public async Task RecoverInterruptedAsync_OnlyRecoversSearchingEntries()
    {
        await using var database = await TestDatabase.CreateAsync();
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));

        await using (var context = new SockseekDbContext(database.Options))
        {
            var store = new SoulseekWishlistStore(context, clock);
            var searching = await store.CreateAsync("searching", 15, enabled: true);
            await store.CreateAsync("idle", 15, enabled: false);
            await store.TryClaimAsync(searching.Id.Value, force: false);
            await store.MarkSubmittedAsync(searching.Id.Value, Guid.NewGuid(), Guid.NewGuid());

            clock.UtcNow = clock.UtcNow.AddMinutes(1);
            Assert.AreEqual(1, await store.RecoverInterruptedAsync());
        }

        await using (var verifyContext = new SockseekDbContext(database.Options))
        {
            var verify = new SoulseekWishlistStore(verifyContext, clock);
            var entries = await verify.ListAsync();
            var recovered = entries.Single(entry => entry.Query == "searching");
            var idle = entries.Single(entry => entry.Query == "idle");
            Assert.AreEqual(WishlistSearchState.Failed, recovered.SearchState);
            Assert.AreEqual("interrupted", recovered.LastErrorCode);
            Assert.AreEqual(clock.UtcNow, recovered.NextSearchAtUtc);
            Assert.AreEqual(WishlistSearchState.NeverRun, idle.SearchState);
        }
    }

    [TestMethod]
    public async Task UpdateAndRemove_PreserveExplicitDisabledState()
    {
        await using var database = await TestDatabase.CreateAsync();
        var clock = new FakeClock(DateTimeOffset.UtcNow);

        await using var context = new SockseekDbContext(database.Options);
        var store = new SoulseekWishlistStore(context, clock);
        var created = await store.CreateAsync("first", 30, enabled: true);
        var updated = await store.UpdateAsync(created.Id.Value, "second", 60, enabled: false);

        Assert.IsNotNull(updated);
        Assert.AreEqual("second", updated.Query);
        Assert.AreEqual(60, updated.IntervalMinutes);
        Assert.IsFalse(updated.Enabled);
        Assert.IsNull(updated.NextSearchAtUtc);
        Assert.IsTrue(await store.RemoveAsync(created.Id.Value));
        Assert.IsFalse(await store.RemoveAsync(created.Id.Value));
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
            var options = new DbContextOptionsBuilder<SockseekDbContext>().UseSqlite(connection).Options;
            await using var setup = new SockseekDbContext(options);
            await setup.Database.MigrateAsync();
            return new TestDatabase(connection, options);
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
