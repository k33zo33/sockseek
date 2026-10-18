using Sockseek.Domain.Common;

namespace Sockseek.Domain.Soulseek;

public sealed class SoulseekWishlistEntry
{
    public const int MinIntervalMinutes = 5;
    public const int MaxIntervalMinutes = 7 * 24 * 60;
    public const int MaxQueryLength = 256;

    private SoulseekWishlistEntry(
        EntityId id,
        string query,
        int intervalMinutes,
        bool enabled,
        WishlistSearchState searchState,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? nextSearchAtUtc,
        DateTimeOffset? lastSearchStartedAtUtc,
        DateTimeOffset? lastSearchCompletedAtUtc,
        Guid? activeWorkflowId,
        Guid? activeJobId,
        int? lastResultCount,
        string? lastErrorCode)
    {
        Id = id;
        Query = NormalizeQuery(query);
        IntervalMinutes = ValidateInterval(intervalMinutes);
        Enabled = enabled;
        SearchState = searchState;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        NextSearchAtUtc = enabled ? nextSearchAtUtc : null;
        LastSearchStartedAtUtc = lastSearchStartedAtUtc;
        LastSearchCompletedAtUtc = lastSearchCompletedAtUtc;
        ActiveWorkflowId = activeWorkflowId;
        ActiveJobId = activeJobId;
        LastResultCount = lastResultCount;
        LastErrorCode = NormalizeErrorCode(lastErrorCode);

        if (searchState == WishlistSearchState.Searching && lastSearchStartedAtUtc == null)
            throw new ArgumentException("A searching wishlist entry must have a start timestamp.", nameof(lastSearchStartedAtUtc));
        if (searchState != WishlistSearchState.Searching && (activeWorkflowId != null || activeJobId != null))
            throw new ArgumentException("Only a searching wishlist entry may reference an active workflow or job.");
        if (lastResultCount < 0)
            throw new ArgumentOutOfRangeException(nameof(lastResultCount));
    }

    public EntityId Id { get; }
    public string Query { get; private set; }
    public int IntervalMinutes { get; private set; }
    public bool Enabled { get; private set; }
    public WishlistSearchState SearchState { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? NextSearchAtUtc { get; private set; }
    public DateTimeOffset? LastSearchStartedAtUtc { get; private set; }
    public DateTimeOffset? LastSearchCompletedAtUtc { get; private set; }
    public Guid? ActiveWorkflowId { get; private set; }
    public Guid? ActiveJobId { get; private set; }
    public int? LastResultCount { get; private set; }
    public string? LastErrorCode { get; private set; }

    public static SoulseekWishlistEntry Create(
        string query,
        int intervalMinutes,
        bool enabled,
        DateTimeOffset now)
        => new(
            EntityId.New(),
            query,
            intervalMinutes,
            enabled,
            WishlistSearchState.NeverRun,
            now,
            now,
            enabled ? now : null,
            null,
            null,
            null,
            null,
            null,
            null);

    public static SoulseekWishlistEntry Rehydrate(
        Guid id,
        string query,
        int intervalMinutes,
        bool enabled,
        WishlistSearchState searchState,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? nextSearchAtUtc,
        DateTimeOffset? lastSearchStartedAtUtc,
        DateTimeOffset? lastSearchCompletedAtUtc,
        Guid? activeWorkflowId,
        Guid? activeJobId,
        int? lastResultCount,
        string? lastErrorCode)
        => new(
            new EntityId(id),
            query,
            intervalMinutes,
            enabled,
            searchState,
            createdAtUtc,
            updatedAtUtc,
            nextSearchAtUtc,
            lastSearchStartedAtUtc,
            lastSearchCompletedAtUtc,
            activeWorkflowId,
            activeJobId,
            lastResultCount,
            lastErrorCode);

    public void Update(string query, int intervalMinutes, bool enabled, DateTimeOffset now)
    {
        string normalizedQuery = NormalizeQuery(query);
        int validatedInterval = ValidateInterval(intervalMinutes);
        bool changed = !string.Equals(Query, normalizedQuery, StringComparison.Ordinal)
            || IntervalMinutes != validatedInterval
            || Enabled != enabled;

        Query = normalizedQuery;
        IntervalMinutes = validatedInterval;
        Enabled = enabled;

        if (changed && SearchState == WishlistSearchState.Searching)
            MarkCancelled(now);

        if (!enabled)
        {
            NextSearchAtUtc = null;
        }
        else if (changed && SearchState != WishlistSearchState.Searching)
        {
            NextSearchAtUtc = now;
        }

        UpdatedAtUtc = now;
    }

    public bool CanStart(DateTimeOffset now, bool force)
        => Enabled
            && SearchState != WishlistSearchState.Searching
            && (force || NextSearchAtUtc == null || NextSearchAtUtc <= now);

    public void MarkClaimed(DateTimeOffset now, bool force = false)
    {
        if (!CanStart(now, force))
            throw new InvalidOperationException("Wishlist entry is not ready to start a search.");

        SearchState = WishlistSearchState.Searching;
        LastSearchStartedAtUtc = now;
        ActiveWorkflowId = null;
        ActiveJobId = null;
        LastErrorCode = null;
        NextSearchAtUtc = now.AddMinutes(IntervalMinutes);
        UpdatedAtUtc = now;
    }

    public void MarkSubmitted(Guid workflowId, Guid jobId, DateTimeOffset now)
    {
        EnsureSearching();
        ActiveWorkflowId = workflowId;
        ActiveJobId = jobId;
        UpdatedAtUtc = now;
    }

    public void MarkSucceeded(int? resultCount, DateTimeOffset now)
    {
        EnsureSearching();
        if (resultCount < 0)
            throw new ArgumentOutOfRangeException(nameof(resultCount));

        SearchState = WishlistSearchState.Succeeded;
        LastResultCount = resultCount;
        LastErrorCode = null;
        Complete(now);
    }

    public void MarkFailed(string errorCode, DateTimeOffset now)
    {
        if (SearchState != WishlistSearchState.Searching)
            throw new InvalidOperationException("Only a searching wishlist entry can fail.");

        SearchState = WishlistSearchState.Failed;
        LastErrorCode = NormalizeErrorCode(errorCode)
            ?? throw new ArgumentException("errorCode is required.", nameof(errorCode));
        Complete(now);
    }

    public void MarkCancelled(DateTimeOffset now)
    {
        if (SearchState != WishlistSearchState.Searching)
            return;

        SearchState = WishlistSearchState.Cancelled;
        LastErrorCode = null;
        Complete(now);
    }

    public void RecoverInterrupted(DateTimeOffset now)
    {
        if (SearchState != WishlistSearchState.Searching)
            return;

        MarkFailed("interrupted", now);
        if (Enabled)
            NextSearchAtUtc = now;
    }

    private void Complete(DateTimeOffset now)
    {
        LastSearchCompletedAtUtc = now;
        ActiveWorkflowId = null;
        ActiveJobId = null;
        UpdatedAtUtc = now;
    }

    private void EnsureSearching()
    {
        if (SearchState != WishlistSearchState.Searching)
            throw new InvalidOperationException("Wishlist entry has not claimed a search.");
    }

    private static string NormalizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("query is required.", nameof(query));

        string normalized = query.Trim();
        if (normalized.Length > MaxQueryLength)
            throw new ArgumentOutOfRangeException(nameof(query), $"query cannot exceed {MaxQueryLength} characters.");

        return normalized;
    }

    private static int ValidateInterval(int intervalMinutes)
    {
        if (intervalMinutes is < MinIntervalMinutes or > MaxIntervalMinutes)
            throw new ArgumentOutOfRangeException(
                nameof(intervalMinutes),
                $"intervalMinutes must be between {MinIntervalMinutes} and {MaxIntervalMinutes}.");

        return intervalMinutes;
    }

    private static string? NormalizeErrorCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
