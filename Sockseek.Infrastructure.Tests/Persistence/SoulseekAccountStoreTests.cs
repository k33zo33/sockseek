using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Common;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class SoulseekAccountStoreTests
{
    [TestMethod]
    public async Task SaveAsync_ReplacesSingletonMetadataWithoutPersistingPassword()
    {
        await using var database = await TestDatabase.CreateAsync();
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
        await using var context = new SockseekDbContext(database.Options);
        var store = new SoulseekAccountStore(context, clock);

        var first = await store.SaveAsync(" listener ", "secret://memory/first");
        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        var updated = await store.SaveAsync("listener-2", "secret://memory/second");
        var loaded = await store.GetAsync();

        Assert.AreEqual("listener", first.Username);
        Assert.AreEqual("listener-2", updated.Username);
        Assert.AreEqual(first.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.AreEqual(clock.UtcNow, updated.UpdatedAtUtc);
        Assert.AreEqual(updated, loaded);
        Assert.AreEqual(1, await context.SoulseekAccounts.CountAsync());

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT Username || '|' || SecretReference FROM SoulseekAccounts WHERE Id = 1;";
        Assert.AreEqual("listener-2|secret://memory/second", await command.ExecuteScalarAsync());

        using var schemaCommand = database.Connection.CreateCommand();
        schemaCommand.CommandText = "PRAGMA table_info('SoulseekAccounts');";
        using var reader = await schemaCommand.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));
        CollectionAssert.DoesNotContain(columns, "Password");
        CollectionAssert.Contains(columns, "SecretReference");
    }

    [TestMethod]
    public async Task RemoveAsync_DeletesOnlyCredentialMetadata()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = new SockseekDbContext(database.Options);
        var store = new SoulseekAccountStore(context, new FakeClock(DateTimeOffset.UtcNow));
        await store.SaveAsync("listener", "secret://memory/reference");

        Assert.IsTrue(await store.RemoveAsync());
        Assert.IsFalse(await store.RemoveAsync());
        Assert.IsNull(await store.GetAsync());
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("bad\nname")]
    public async Task SaveAsync_InvalidUsernameIsRejected(string username)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = new SockseekDbContext(database.Options);
        var store = new SoulseekAccountStore(context, new FakeClock(DateTimeOffset.UtcNow));

        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            store.SaveAsync(username, "secret://memory/reference"));
        Assert.AreEqual(0, await context.SoulseekAccounts.CountAsync());
    }

    [TestMethod]
    public async Task SaveAsync_NonOpaqueSecretReferenceIsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = new SockseekDbContext(database.Options);
        var store = new SoulseekAccountStore(context, new FakeClock(DateTimeOffset.UtcNow));

        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            store.SaveAsync("listener", "plain-password"));
        Assert.AreEqual(0, await context.SoulseekAccounts.CountAsync());
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, DbContextOptions<SockseekDbContext> options)
        {
            Connection = connection;
            Options = options;
        }

        public SqliteConnection Connection { get; }
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

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
}
