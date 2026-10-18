using Sockseek.Domain.Soulseek;

namespace Sockseek.Application.Soulseek;

public interface ISoulseekWishlistStore
{
    Task<IReadOnlyList<SoulseekWishlistEntry>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SoulseekWishlistEntry>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SoulseekWishlistEntry>> ListSearchingAsync(CancellationToken cancellationToken = default);
    Task<SoulseekWishlistEntry?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SoulseekWishlistEntry> CreateAsync(string query, int intervalMinutes, bool enabled, CancellationToken cancellationToken = default);
    Task<SoulseekWishlistEntry?> UpdateAsync(Guid id, string query, int intervalMinutes, bool enabled, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SoulseekWishlistEntry?> TryClaimAsync(Guid id, bool force, CancellationToken cancellationToken = default);
    Task MarkSubmittedAsync(Guid id, Guid workflowId, Guid jobId, CancellationToken cancellationToken = default);
    Task MarkSucceededAsync(Guid id, int? resultCount, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid id, string errorCode, CancellationToken cancellationToken = default);
    Task MarkCancelledAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken = default);
}
