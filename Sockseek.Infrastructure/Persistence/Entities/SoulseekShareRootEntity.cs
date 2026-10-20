using Sockseek.Infrastructure.Persistence.Abstractions;

namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class SoulseekShareRootEntity : IHasConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public string Path { get; set; } = string.Empty;
    public string VirtualName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
