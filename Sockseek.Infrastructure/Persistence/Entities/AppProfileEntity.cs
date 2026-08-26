namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class AppProfileEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public bool Active { get; set; }
}
