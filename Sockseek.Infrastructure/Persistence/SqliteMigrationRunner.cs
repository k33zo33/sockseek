using Microsoft.EntityFrameworkCore;

namespace Sockseek.Infrastructure.Persistence;

public sealed class SqliteMigrationRunner(Func<SockseekDbContext> dbContextFactory)
{
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

        await using var dbContext = dbContextFactory();
        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        string? backupPath = null;
        if (File.Exists(fullDatabasePath) && pendingMigrations.Length > 0)
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
}
