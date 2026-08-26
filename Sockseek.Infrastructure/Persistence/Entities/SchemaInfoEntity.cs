namespace Sockseek.Infrastructure.Persistence.Entities;

using Sockseek.Infrastructure.Persistence.Abstractions;

public sealed class SchemaInfoEntity : IHasConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public string ApplicationVersion { get; set; } = string.Empty;
    public string MigrationVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastBackupUtc { get; set; }
}
