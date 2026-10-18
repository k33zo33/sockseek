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
        {
            await EnableWriteAheadLoggingAsync(dbContext, cancellationToken);
            return new SqliteMigrationRunResult(pendingMigrations, BackupPath: null);
        }

        await ClearAbandonedMigrationLockAsync(dbContext, cancellationToken);

        string? backupPath = null;
        if (databaseExisted)
        {
            Directory.CreateDirectory(fullBackupDirectory);
            backupPath = Path.Combine(
                fullBackupDirectory,
                $"{Path.GetFileNameWithoutExtension(fullDatabasePath)}-{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(fullDatabasePath)}.bak");
            await BackupDatabaseAsync(fullDatabasePath, backupPath, cancellationToken);
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
        await EnableWriteAheadLoggingAsync(dbContext, cancellationToken);
        return new SqliteMigrationRunResult(pendingMigrations, backupPath);
    }

    private static async Task BackupDatabaseAsync(
        string sourcePath,
        string backupPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var source = new SqliteConnection($"Data Source={sourcePath}");
        await using var destination = new SqliteConnection($"Data Source={backupPath}");
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    private static async Task EnableWriteAheadLoggingAsync(
        SockseekDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            string? journalMode = Convert.ToString(
                await command.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture);
            if (!string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"SQLite refused write-ahead logging and returned journal mode '{journalMode ?? "<null>"}'.");
            }
        }
        finally
        {
            if (shouldClose)
                await dbContext.Database.CloseConnectionAsync();
        }
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
