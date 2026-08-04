namespace Sockseek.Infrastructure.Persistence;

public sealed record SqliteMigrationRunResult(
    IReadOnlyList<string> AppliedMigrations,
    string? BackupPath);
