using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public class SqliteMigrationRunnerTests
{
    [TestMethod]
    public async Task MigrateAsync_FromPreviousPublicSchema_AddsWishlistAndPreservesExistingData()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sockseek-migration-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string databasePath = Path.Combine(tempDir, "app.db");
        string backupDirectory = Path.Combine(tempDir, "backups");

        try
        {
            await using (var previousContext = CreateContext(databasePath))
            {
                var migrator = previousContext.Database.GetService<IMigrator>();
                await migrator.MigrateAsync("20260918183717_AddPlaybackQueuePersistence");
                previousContext.AppSettings.Add(new AppSettingEntity
                {
                    Key = "upgrade-marker",
                    JsonValue = "\"preserved\"",
                    UpdatedAtUtc = new DateTimeOffset(2026, 10, 18, 14, 0, 0, TimeSpan.Zero),
                });
                await previousContext.SaveChangesAsync();
            }

            var runner = new SqliteMigrationRunner(() => CreateContext(databasePath));
            var result = await runner.MigrateAsync(databasePath, backupDirectory);

            CollectionAssert.AreEqual(
                new[] { "20261018091320_AddSoulseekWishlist" },
                result.AppliedMigrations.ToArray());
            Assert.IsNotNull(result.BackupPath);
            Assert.IsTrue(File.Exists(result.BackupPath));

            await using var migratedConnection = new SqliteConnection($"Data Source={databasePath}");
            await migratedConnection.OpenAsync();
            CollectionAssert.Contains(await TableNamesAsync(migratedConnection), "SoulseekWishlistEntries");

            using var markerCommand = migratedConnection.CreateCommand();
            markerCommand.CommandText = "SELECT JsonValue FROM AppSettings WHERE Key = 'upgrade-marker';";
            Assert.AreEqual("\"preserved\"", await markerCommand.ExecuteScalarAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task MigrateAsync_ExistingDatabaseWithPendingMigrations_CreatesBackupBeforeMigrating()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sockseek-migration-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string databasePath = Path.Combine(tempDir, "app.db");
        string backupDirectory = Path.Combine(tempDir, "backups");

        try
        {
            await using (var seedConnection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await seedConnection.OpenAsync();
                using var command = seedConnection.CreateCommand();
                command.CommandText = "CREATE TABLE LegacyMarker (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL); INSERT INTO LegacyMarker (Name) VALUES ('legacy');";
                await command.ExecuteNonQueryAsync();
            }

            var runner = new SqliteMigrationRunner(() => CreateContext(databasePath));
            var result = await runner.MigrateAsync(databasePath, backupDirectory);

            Assert.AreEqual(8, result.AppliedMigrations.Count);
            Assert.IsNotNull(result.BackupPath);
            Assert.IsTrue(File.Exists(result.BackupPath));

            await using var backupConnection = new SqliteConnection($"Data Source={result.BackupPath}");
            await backupConnection.OpenAsync();
            CollectionAssert.Contains(await TableNamesAsync(backupConnection), "LegacyMarker");
            CollectionAssert.DoesNotContain(await TableNamesAsync(backupConnection), "ExternalAccounts");

            await using var migratedConnection = new SqliteConnection($"Data Source={databasePath}");
            await migratedConnection.OpenAsync();
            CollectionAssert.Contains(await TableNamesAsync(migratedConnection), "ExternalAccounts");
            CollectionAssert.Contains(await TableNamesAsync(migratedConnection), "LibraryRoots");
            CollectionAssert.Contains(await TableNamesAsync(migratedConnection), "SoulseekWishlistEntries");
            CollectionAssert.Contains(await TableNamesAsync(migratedConnection), "LegacyMarker");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task MigrateAsync_WhenNoPendingMigrationsAndLockIsAbandoned_ReturnsWithoutWaitingOrBackup()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sockseek-migration-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string databasePath = Path.Combine(tempDir, "app.db");
        string backupDirectory = Path.Combine(tempDir, "backups");

        try
        {
            await using (var initialContext = CreateContext(databasePath))
            {
                await initialContext.Database.MigrateAsync();
            }
            await InsertMigrationLockAsync(databasePath, "2000-01-01 00:00:00+00:00");

            var runner = new SqliteMigrationRunner(() => CreateContext(databasePath));
            var result = await runner.MigrateAsync(databasePath, backupDirectory).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(0, result.AppliedMigrations.Count);
            Assert.IsNull(result.BackupPath);
            Assert.IsFalse(Directory.Exists(backupDirectory));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task MigrateAsync_WithPendingMigrationsAndAbandonedLock_ClearsLockAndMigrates()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sockseek-migration-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string databasePath = Path.Combine(tempDir, "app.db");
        string backupDirectory = Path.Combine(tempDir, "backups");

        try
        {
            await using (var seedConnection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await seedConnection.OpenAsync();
                using var command = seedConnection.CreateCommand();
                command.CommandText = "CREATE TABLE LegacyMarker (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);";
                await command.ExecuteNonQueryAsync();
            }
            await InsertMigrationLockAsync(databasePath, "2000-01-01 00:00:00+00:00");

            var runner = new SqliteMigrationRunner(() => CreateContext(databasePath));
            var result = await runner.MigrateAsync(databasePath, backupDirectory).WaitAsync(TimeSpan.FromSeconds(15));

            Assert.AreEqual(8, result.AppliedMigrations.Count);
            Assert.IsNotNull(result.BackupPath);
            await using var migratedConnection = new SqliteConnection($"Data Source={databasePath}");
            await migratedConnection.OpenAsync();
            CollectionAssert.Contains(await TableNamesAsync(migratedConnection), "ExternalAccounts");
            CollectionAssert.Contains(await TableNamesAsync(migratedConnection), "SoulseekWishlistEntries");
            Assert.AreEqual(0L, await MigrationLockCountAsync(migratedConnection));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private static SockseekDbContext CreateContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        return new SockseekDbContext(options);
    }

    private static async Task<string[]> TableNamesAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));

        return names.ToArray();
    }

    private static async Task InsertMigrationLockAsync(string databasePath, string timestamp)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsLock" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK___EFMigrationsLock" PRIMARY KEY,
                "Timestamp" TEXT NOT NULL
            );
            INSERT OR REPLACE INTO "__EFMigrationsLock" ("Id", "Timestamp") VALUES (1, $timestamp);
            """;
        command.Parameters.AddWithValue("$timestamp", timestamp);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> MigrationLockCountAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsLock\";";
        return (long)(await command.ExecuteScalarAsync() ?? -1L);
    }
}
