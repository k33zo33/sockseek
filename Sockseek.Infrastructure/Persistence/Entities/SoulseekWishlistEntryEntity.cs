using Sockseek.Domain.Soulseek;
using Sockseek.Infrastructure.Persistence.Abstractions;

namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class SoulseekWishlistEntryEntity : IHasConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public string Query { get; set; } = string.Empty;
    public int IntervalMinutes { get; set; }
    public bool Enabled { get; set; }
    public WishlistSearchState SearchState { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? NextSearchAtUtc { get; set; }
    public long? NextSearchAtUtcUnixMilliseconds { get; set; }
    public DateTimeOffset? LastSearchStartedAtUtc { get; set; }
    public DateTimeOffset? LastSearchCompletedAtUtc { get; set; }
    public Guid? ActiveWorkflowId { get; set; }
    public Guid? ActiveJobId { get; set; }
    public int? LastResultCount { get; set; }
    public string? LastErrorCode { get; set; }
}
