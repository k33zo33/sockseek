using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.Tests.Persistence;

[TestClass]
public class SqliteMigrationRunnerTests
{
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

            Assert.AreEqual(2, result.AppliedMigrations.Count);
            Assert.IsNotNull(result.BackupPath);
            Assert.IsTrue(File.Exists(result.BackupPath));

            await using var backupConnection = new SqliteConnection($"Data Source={result.BackupPath}");
            await backupConnection.OpenAsync();
            CollectionAssert.Contains(await TableNamesAsync(backupConnection), "LegacyMarker");
            CollectionAssert.DoesNotContain(await TableNamesAsync(backupConnection), "ExternalAccounts");

            await using var migratedConnection = new SqliteConnection($"Data Source={databasePath}");
            await migratedConnection.OpenAsync();
            CollectionAssert.Contains(await TableNamesAsync(migratedConnection), "ExternalAccounts");
            CollectionAssert.Contains(await TableNamesAsync(migratedConnection), "LegacyMarker");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task MigrateAsync_WhenNoPendingMigrations_DoesNotCreateBackup()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "sockseek-migration-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string databasePath = Path.Combine(tempDir, "app.db");
        string backupDirectory = Path.Combine(tempDir, "backups");

        try
        {
            await using (var initialContext = CreateContext(databasePath))
                await initialContext.Database.MigrateAsync();

            var runner = new SqliteMigrationRunner(() => CreateContext(databasePath));
            var result = await runner.MigrateAsync(databasePath, backupDirectory);

            Assert.AreEqual(0, result.AppliedMigrations.Count);
            Assert.IsNull(result.BackupPath);
            Assert.IsFalse(Directory.Exists(backupDirectory));
        }
        finally
        {
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
}
