using Sockseek.Infrastructure.Persistence.Abstractions;

namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class SoulseekAccountEntity : IHasConcurrencyToken
{
    public int Id { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public string Username { get; set; } = string.Empty;
    public string SecretReference { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
