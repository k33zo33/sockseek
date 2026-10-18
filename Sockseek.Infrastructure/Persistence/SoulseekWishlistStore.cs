using Microsoft.EntityFrameworkCore;
using Sockseek.Application.Common;
using Sockseek.Application.Soulseek;
using Sockseek.Domain.Soulseek;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class SoulseekWishlistStore(SockseekDbContext dbContext, IClock clock) : ISoulseekWishlistStore
{
    public async Task<IReadOnlyList<SoulseekWishlistEntry>> ListAsync(CancellationToken cancellationToken = default)
        => (await dbContext.SoulseekWishlistEntries
                .AsNoTracking()
                .OrderByDescending(entity => entity.Enabled)
                .ThenBy(entity => entity.Query)
                .ToListAsync(cancellationToken))
            .Select(ToDomain)
            .ToArray();

    public async Task<IReadOnlyList<SoulseekWishlistEntry>> ListDueAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            return [];

        long nowUnixMilliseconds = now.ToUnixTimeMilliseconds();
        return (await dbContext.SoulseekWishlistEntries
                .AsNoTracking()
                .Where(entity => entity.Enabled
                    && entity.SearchState != WishlistSearchState.Searching
                    && (entity.NextSearchAtUtcUnixMilliseconds == null
                        || entity.NextSearchAtUtcUnixMilliseconds <= nowUnixMilliseconds))
                .OrderBy(entity => entity.NextSearchAtUtcUnixMilliseconds)
                .ThenBy(entity => entity.Id)
                .Take(limit)
                .ToListAsync(cancellationToken))
            .Select(ToDomain)
            .ToArray();
    }

    public async Task<IReadOnlyList<SoulseekWishlistEntry>> ListSearchingAsync(CancellationToken cancellationToken = default)
        => (await dbContext.SoulseekWishlistEntries
                .AsNoTracking()
                .Where(entity => entity.SearchState == WishlistSearchState.Searching)
                .OrderBy(entity => entity.Id)
                .ToListAsync(cancellationToken))
            .Select(ToDomain)
            .ToArray();

    public async Task<SoulseekWishlistEntry?> FindAsync(Guid id, CancellationToken cancellationToken = default)
        => await dbContext.SoulseekWishlistEntries
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => ToDomain(entity))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SoulseekWishlistEntry> CreateAsync(
        string query,
        int intervalMinutes,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var entry = SoulseekWishlistEntry.Create(query, intervalMinutes, enabled, clock.UtcNow);
        dbContext.SoulseekWishlistEntries.Add(ToEntity(entry));
        await dbContext.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<SoulseekWishlistEntry?> UpdateAsync(
        Guid id,
        string query,
        int intervalMinutes,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SoulseekWishlistEntries.FindAsync([id], cancellationToken);
        if (entity == null)
            return null;

        var entry = ToDomain(entity);
        entry.Update(query, intervalMinutes, enabled, clock.UtcNow);
        Apply(entry, entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SoulseekWishlistEntries.FindAsync([id], cancellationToken);
        if (entity == null)
            return false;

        dbContext.SoulseekWishlistEntries.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SoulseekWishlistEntry?> TryClaimAsync(
        Guid id,
        bool force,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SoulseekWishlistEntries.FindAsync([id], cancellationToken);
        if (entity == null)
            return null;

        var entry = ToDomain(entity);
        DateTimeOffset now = clock.UtcNow;
        if (!entry.CanStart(now, force))
            return null;

        entry.MarkClaimed(now, force);
        Apply(entry, entity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return entry;
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
    }

    public Task MarkSubmittedAsync(Guid id, Guid workflowId, Guid jobId, CancellationToken cancellationToken = default)
        => UpdateRequiredAsync(id, entry => entry.MarkSubmitted(workflowId, jobId, clock.UtcNow), cancellationToken);

    public Task MarkSucceededAsync(Guid id, int? resultCount, CancellationToken cancellationToken = default)
        => UpdateRequiredAsync(id, entry => entry.MarkSucceeded(resultCount, clock.UtcNow), cancellationToken);

    public Task MarkFailedAsync(Guid id, string errorCode, CancellationToken cancellationToken = default)
        => UpdateRequiredAsync(id, entry => entry.MarkFailed(errorCode, clock.UtcNow), cancellationToken);

    public Task MarkCancelledAsync(Guid id, CancellationToken cancellationToken = default)
        => UpdateRequiredAsync(id, entry => entry.MarkCancelled(clock.UtcNow), cancellationToken);

    public async Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.SoulseekWishlistEntries
            .Where(entity => entity.SearchState == WishlistSearchState.Searching)
            .ToListAsync(cancellationToken);

        DateTimeOffset now = clock.UtcNow;
        foreach (var entity in entities)
        {
            var entry = ToDomain(entity);
            entry.RecoverInterrupted(now);
            Apply(entry, entity);
        }

        if (entities.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        return entities.Count;
    }

    private async Task UpdateRequiredAsync(
        Guid id,
        Action<SoulseekWishlistEntry> update,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.SoulseekWishlistEntries.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException("Wishlist entry was not found.");
        var entry = ToDomain(entity);
        update(entry);
        Apply(entry, entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static SoulseekWishlistEntry ToDomain(SoulseekWishlistEntryEntity entity)
        => SoulseekWishlistEntry.Rehydrate(
            entity.Id,
            entity.Query,
            entity.IntervalMinutes,
            entity.Enabled,
            entity.SearchState,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.NextSearchAtUtc,
            entity.LastSearchStartedAtUtc,
            entity.LastSearchCompletedAtUtc,
            entity.ActiveWorkflowId,
            entity.ActiveJobId,
            entity.LastResultCount,
            entity.LastErrorCode);

    private static SoulseekWishlistEntryEntity ToEntity(SoulseekWishlistEntry entry)
    {
        var entity = new SoulseekWishlistEntryEntity { Id = entry.Id.Value };
        Apply(entry, entity);
        return entity;
    }

    private static void Apply(SoulseekWishlistEntry entry, SoulseekWishlistEntryEntity entity)
    {
        entity.Query = entry.Query;
        entity.IntervalMinutes = entry.IntervalMinutes;
        entity.Enabled = entry.Enabled;
        entity.SearchState = entry.SearchState;
        entity.CreatedAtUtc = entry.CreatedAtUtc;
        entity.UpdatedAtUtc = entry.UpdatedAtUtc;
        entity.NextSearchAtUtc = entry.NextSearchAtUtc;
        entity.NextSearchAtUtcUnixMilliseconds = entry.NextSearchAtUtc?.ToUnixTimeMilliseconds();
        entity.LastSearchStartedAtUtc = entry.LastSearchStartedAtUtc;
        entity.LastSearchCompletedAtUtc = entry.LastSearchCompletedAtUtc;
        entity.ActiveWorkflowId = entry.ActiveWorkflowId;
        entity.ActiveJobId = entry.ActiveJobId;
        entity.LastResultCount = entry.LastResultCount;
        entity.LastErrorCode = entry.LastErrorCode;
    }
}
