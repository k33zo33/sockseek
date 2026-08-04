namespace Sockseek.Infrastructure.Persistence.Entities;

using Sockseek.Infrastructure.Persistence.Abstractions;

public sealed class ProviderSyncStateEntity : IHasConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public int Provider { get; set; }
    public Guid? AccountId { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string? Cursor { get; set; }
    public string? ETag { get; set; }
    public DateTimeOffset? LastSuccessUtc { get; set; }
    public string? LastError { get; set; }
}
