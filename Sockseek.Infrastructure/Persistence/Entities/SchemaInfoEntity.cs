namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class SchemaInfoEntity
{
    public Guid Id { get; set; }
    public string ApplicationVersion { get; set; } = string.Empty;
    public string MigrationVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastBackupUtc { get; set; }
}
