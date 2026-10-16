using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public class ServerDatabaseMigrationServiceTests
{
    [TestMethod]
    public async Task EnsureMigratedAsync_WithConfigDir_CreatesDatabaseUnderConfigDirWithoutBackup()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "sockseek-server-migration-" + Guid.NewGuid().ToString("N"));
        string configDir = Path.Combine(tempRoot, "config");
        string databasePath = Path.Combine(configDir, "sockseek.db");
        string backupDirectory = Path.Combine(configDir, "backups");

        try
        {
            var service = CreateService(new ServerOptions { ConfigDir = configDir });

            await service.EnsureMigratedAsync();

            Assert.IsTrue(File.Exists(databasePath));
            Assert.IsFalse(Directory.Exists(backupDirectory));

            await using var migratedConnection = new SqliteConnection($"Data Source={databasePath}");
            await migratedConnection.OpenAsync();
            Assert.IsTrue(await TableExistsAsync(migratedConnection, "ExternalAccounts"));
            Assert.IsTrue(await TableExistsAsync(migratedConnection, "LibraryRoots"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task EnsureMigratedAsync_ExistingDatabaseWithPendingMigrations_CreatesBackupUnderConfiguredBackupDir()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "sockseek-server-migration-" + Guid.NewGuid().ToString("N"));
        string configDir = Path.Combine(tempRoot, "config");
        string backupDirectory = Path.Combine(tempRoot, "upgrade-backups");
        string databasePath = Path.Combine(configDir, "sockseek.db");

        try
        {
            Directory.CreateDirectory(configDir);
            await CreateLegacyDatabaseAsync(databasePath);

            var service = CreateService(new ServerOptions
            {
                ConfigDir = configDir,
                DatabaseBackupDir = backupDirectory
            });

            await service.EnsureMigratedAsync();

            string[] backups = Directory.GetFiles(backupDirectory, "sockseek-*.db.bak");
            Assert.AreEqual(1, backups.Length);

            await using var backupConnection = new SqliteConnection($"Data Source={backups[0]}");
            await backupConnection.OpenAsync();
            Assert.IsTrue(await TableExistsAsync(backupConnection, "LegacyMarker"));
            Assert.IsFalse(await TableExistsAsync(backupConnection, "ExternalAccounts"));

            await using var migratedConnection = new SqliteConnection($"Data Source={databasePath}");
            await migratedConnection.OpenAsync();
            Assert.IsTrue(await TableExistsAsync(migratedConnection, "LegacyMarker"));
            Assert.IsTrue(await TableExistsAsync(migratedConnection, "ExternalAccounts"));
            Assert.IsTrue(await TableExistsAsync(migratedConnection, "LibraryRoots"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task HostedService_StartAsync_CurrentSchemaWithAbandonedLock_CompletesReadiness()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "sockseek-server-migration-" + Guid.NewGuid().ToString("N"));
        string configDir = Path.Combine(tempRoot, "config");
        string databasePath = Path.Combine(configDir, "sockseek.db");

        try
        {
            var options = Options.Create(new ServerOptions { ConfigDir = configDir });
            var migrationService = new ServerDatabaseMigrationService(options);
            await migrationService.EnsureMigratedAsync();
            await InsertMigrationLockAsync(databasePath, "2000-01-01 00:00:00+00:00");

            var restartedMigrationService = new ServerDatabaseMigrationService(options);
            var hostedService = new ServerDatabaseMigrationHostedService(restartedMigrationService, options);

            await hostedService.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

            await using var context = new SqliteConnection($"Data Source={databasePath}");
            await context.OpenAsync();
            Assert.IsTrue(await TableExistsAsync(context, "ExternalAccounts"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTempRoot(tempRoot);
        }
    }

    private static ServerDatabaseMigrationService CreateService(ServerOptions options)
    {
        return new ServerDatabaseMigrationService(Options.Create(options));
    }

    private static async Task CreateLegacyDatabaseAsync(string databasePath)
    {
        await using var seedConnection = new SqliteConnection($"Data Source={databasePath}");
        await seedConnection.OpenAsync();
        using var command = seedConnection.CreateCommand();
        command.CommandText = "CREATE TABLE LegacyMarker (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL); INSERT INTO LegacyMarker (Name) VALUES ('legacy');";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        object? result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) == 1;
    }

    private static async Task InsertMigrationLockAsync(string databasePath, string timestamp)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO \"__EFMigrationsLock\" (\"Id\", \"Timestamp\") VALUES (1, $timestamp);";
        command.Parameters.AddWithValue("$timestamp", timestamp);
        await command.ExecuteNonQueryAsync();
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, recursive: true);
    }
}
