using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Server;

public sealed class ServerDatabaseMigrationService(IOptions<ServerOptions> options)
{
    private readonly SemaphoreSlim migrationLock = new(1, 1);
    private bool migrated;

    public async Task EnsureMigratedAsync(CancellationToken cancellationToken = default)
    {
        if (migrated)
            return;

        await migrationLock.WaitAsync(cancellationToken);
        try
        {
            if (migrated)
                return;

            string databasePath = ResolveDatabasePath();
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            string backupDirectory = options.Value.DatabaseBackupDir
                ?? Path.Combine(Path.GetDirectoryName(databasePath)!, "backups");
            var runner = new SqliteMigrationRunner(CreateContext);
            await runner.MigrateAsync(databasePath, backupDirectory, cancellationToken);
            migrated = true;
        }
        finally
        {
            migrationLock.Release();
        }
    }

    private SockseekDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<SockseekDbContext>();
        builder.UseSqlite($"Data Source={ResolveDatabasePath()}");
        return new SockseekDbContext(builder.Options);
    }

    private string ResolveDatabasePath()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.DatabasePath))
            return Path.GetFullPath(options.Value.DatabasePath);

        string baseDirectory = !string.IsNullOrWhiteSpace(options.Value.ConfigDir)
            ? options.Value.ConfigDir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sockseek");
        return Path.GetFullPath(Path.Combine(baseDirectory, "sockseek.db"));
    }
}
