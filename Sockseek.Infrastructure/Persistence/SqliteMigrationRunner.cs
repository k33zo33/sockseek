using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;

namespace Sockseek.Infrastructure.Persistence;

public sealed class SqliteMigrationRunner(Func<SockseekDbContext> dbContextFactory)
{
    private static readonly TimeSpan AbandonedMigrationLockAge = TimeSpan.FromMinutes(15);

    public async Task<SqliteMigrationRunResult> MigrateAsync(
        string databasePath,
        string backupDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("databasePath is required.", nameof(databasePath));
        if (string.IsNullOrWhiteSpace(backupDirectory))
            throw new ArgumentException("backupDirectory is required.", nameof(backupDirectory));

        string fullDatabasePath = Path.GetFullPath(databasePath);
        string fullBackupDirectory = Path.GetFullPath(backupDirectory);
        bool databaseExisted = File.Exists(fullDatabasePath);

        await using var dbContext = dbContextFactory();
        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pendingMigrations.Length == 0)
            return new SqliteMigrationRunResult(pendingMigrations, BackupPath: null);

        await ClearAbandonedMigrationLockAsync(dbContext, cancellationToken);

        string? backupPath = null;
        if (databaseExisted)
        {
            Directory.CreateDirectory(fullBackupDirectory);
            backupPath = Path.Combine(
                fullBackupDirectory,
                $"{Path.GetFileNameWithoutExtension(fullDatabasePath)}-{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(fullDatabasePath)}.bak");
            File.Copy(fullDatabasePath, backupPath, overwrite: true);
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
        return new SqliteMigrationRunResult(pendingMigrations, backupPath);
    }

    private static async Task ClearAbandonedMigrationLockAsync(
        SockseekDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var findLock = connection.CreateCommand();
            findLock.CommandText = """
                SELECT "Timestamp"
                FROM "__EFMigrationsLock"
                WHERE "Id" = 1
                LIMIT 1;
                """;

            object? value;
            try
            {
                value = await findLock.ExecuteScalarAsync(cancellationToken);
            }
            catch (SqliteException exception) when (IsMissingMigrationLockTable(exception))
            {
                return;
            }

            if (value is not string timestamp
                || !DateTimeOffset.TryParse(
                    timestamp,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var lockCreatedAt)
                || DateTimeOffset.UtcNow - lockCreatedAt < AbandonedMigrationLockAge)
            {
                return;
            }

            await using var clearLock = connection.CreateCommand();
            clearLock.CommandText = "DELETE FROM \"__EFMigrationsLock\" WHERE \"Id\" = 1;";
            await clearLock.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
                await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static bool IsMissingMigrationLockTable(SqliteException exception)
        => exception.SqliteErrorCode == 1
            && exception.Message.Contains("no such table: __EFMigrationsLock", StringComparison.OrdinalIgnoreCase);
}
