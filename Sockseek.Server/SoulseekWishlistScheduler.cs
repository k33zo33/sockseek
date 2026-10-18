using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sockseek.Application.Common;
using Sockseek.Application.Soulseek;
using Sockseek.Domain.Soulseek;

namespace Sockseek.Server;

public sealed record WishlistRunResult(
    bool Found,
    bool Started,
    SoulseekWishlistEntry? Entry);

public sealed class SoulseekWishlistScheduler : BackgroundService
{
    private const int DefaultMaximumConcurrentSearches = 4;
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ISoulseekEngineGateway gateway;
    private readonly IClock clock;
    private readonly ILogger<SoulseekWishlistScheduler> logger;
    private readonly ServerDatabaseMigrationService? databaseMigration;
    private readonly bool shouldMigrate;
    private readonly int maximumConcurrentSearches;
    private readonly TimeSpan pollInterval;
    private readonly SemaphoreSlim operationLock = new(1, 1);

    public SoulseekWishlistScheduler(
        IServiceScopeFactory scopeFactory,
        ISoulseekEngineGateway gateway,
        IClock clock,
        ILogger<SoulseekWishlistScheduler> logger,
        ServerDatabaseMigrationService databaseMigration,
        IOptions<ServerOptions> serverOptions)
        : this(
            scopeFactory,
            gateway,
            clock,
            logger,
            databaseMigration,
            ServerDatabaseMigrationHostedService.ShouldMigrate(serverOptions.Value),
            DefaultMaximumConcurrentSearches,
            DefaultPollInterval)
    {
    }

    internal SoulseekWishlistScheduler(
        IServiceScopeFactory scopeFactory,
        ISoulseekEngineGateway gateway,
        IClock clock,
        ILogger<SoulseekWishlistScheduler> logger,
        int maximumConcurrentSearches = DefaultMaximumConcurrentSearches,
        TimeSpan? pollInterval = null)
        : this(
            scopeFactory,
            gateway,
            clock,
            logger,
            databaseMigration: null,
            shouldMigrate: false,
            maximumConcurrentSearches,
            pollInterval ?? DefaultPollInterval)
    {
    }

    private SoulseekWishlistScheduler(
        IServiceScopeFactory scopeFactory,
        ISoulseekEngineGateway gateway,
        IClock clock,
        ILogger<SoulseekWishlistScheduler> logger,
        ServerDatabaseMigrationService? databaseMigration,
        bool shouldMigrate,
        int maximumConcurrentSearches,
        TimeSpan pollInterval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumConcurrentSearches, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);

        this.scopeFactory = scopeFactory;
        this.gateway = gateway;
        this.clock = clock;
        this.logger = logger;
        this.databaseMigration = databaseMigration;
        this.shouldMigrate = shouldMigrate;
        this.maximumConcurrentSearches = maximumConcurrentSearches;
        this.pollInterval = pollInterval;
    }

    public async Task<IReadOnlyList<SoulseekWishlistEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureMigratedAsync(cancellationToken);
        using var scope = scopeFactory.CreateScope();
        return await GetStore(scope).ListAsync(cancellationToken);
    }

    public async Task<SoulseekWishlistEntry> CreateAsync(
        string query,
        int intervalMinutes,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        await EnsureMigratedAsync(cancellationToken);
        using var scope = scopeFactory.CreateScope();
        return await GetStore(scope).CreateAsync(query, intervalMinutes, enabled, cancellationToken);
    }

    public async Task<SoulseekWishlistEntry?> UpdateAsync(
        Guid id,
        string query,
        int intervalMinutes,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        await EnsureMigratedAsync(cancellationToken);
        _ = SoulseekWishlistEntry.Create(query, intervalMinutes, enabled, clock.UtcNow);
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var store = GetStore(scope);
            var existing = await store.FindAsync(id, cancellationToken);
            if (existing == null)
                return null;

            bool changed = !string.Equals(existing.Query, query.Trim(), StringComparison.Ordinal)
                || existing.IntervalMinutes != intervalMinutes
                || existing.Enabled != enabled;
            if (changed && existing.ActiveJobId is { } jobId)
            {
                await TryCancelJobAsync(jobId, cancellationToken);
                await store.MarkCancelledAsync(id, cancellationToken);
            }

            return await store.UpdateAsync(id, query, intervalMinutes, enabled, cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureMigratedAsync(cancellationToken);
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var store = GetStore(scope);
            var existing = await store.FindAsync(id, cancellationToken);
            if (existing == null)
                return false;

            if (existing.ActiveJobId is { } jobId)
                await TryCancelJobAsync(jobId, cancellationToken);

            return await store.RemoveAsync(id, cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task<WishlistRunResult> RunNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureMigratedAsync(cancellationToken);
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var store = GetStore(scope);
            var existing = await store.FindAsync(id, cancellationToken);
            if (existing == null)
                return new WishlistRunResult(false, false, null);
            if (!existing.Enabled || existing.SearchState == WishlistSearchState.Searching)
                return new WishlistRunResult(true, false, existing);
            if ((await store.ListSearchingAsync(cancellationToken)).Count >= maximumConcurrentSearches)
                return new WishlistRunResult(true, false, existing);

            var claimed = await store.TryClaimAsync(id, force: true, cancellationToken);
            if (claimed == null)
                return new WishlistRunResult(true, false, await store.FindAsync(id, cancellationToken));

            await StartClaimedAsync(store, claimed, cancellationToken);
            return new WishlistRunResult(true, true, await store.FindAsync(id, cancellationToken));
        }
        finally
        {
            operationLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!shouldMigrate)
            return;

        await EnsureMigratedAsync(stoppingToken);
        using (var scope = scopeFactory.CreateScope())
            await GetStore(scope).RecoverInterruptedAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Wishlist scheduler iteration failed with {ExceptionType}.",
                    ex.GetType().Name);
            }

            await Task.Delay(pollInterval, stoppingToken);
        }
    }

    internal async Task ProcessOnceAsync(CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var store = GetStore(scope);
            int activeCount = await ReconcileSearchingAsync(store, cancellationToken);
            int availableSlots = maximumConcurrentSearches - activeCount;
            if (availableSlots <= 0)
                return;

            var due = await store.ListDueAsync(clock.UtcNow, availableSlots, cancellationToken);
            foreach (var candidate in due)
            {
                var claimed = await store.TryClaimAsync(candidate.Id.Value, force: false, cancellationToken);
                if (claimed != null)
                    await StartClaimedAsync(store, claimed, cancellationToken);
            }
        }
        finally
        {
            operationLock.Release();
        }
    }

    private async Task<int> ReconcileSearchingAsync(
        ISoulseekWishlistStore store,
        CancellationToken cancellationToken)
    {
        int activeCount = 0;
        foreach (var entry in await store.ListSearchingAsync(cancellationToken))
        {
            if (entry.ActiveJobId is not { } jobId)
            {
                await store.MarkFailedAsync(entry.Id.Value, "submission_interrupted", cancellationToken);
                continue;
            }

            JobSnapshot? snapshot;
            try
            {
                snapshot = await gateway.GetJobAsync(jobId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Wishlist job status check failed with {ExceptionType}.",
                    ex.GetType().Name);
                activeCount++;
                continue;
            }

            if (snapshot == null)
            {
                await store.MarkFailedAsync(entry.Id.Value, "job_missing", cancellationToken);
                continue;
            }

            switch (snapshot.State)
            {
                case SoulseekJobState.Queued:
                case SoulseekJobState.Running:
                    activeCount++;
                    break;
                case SoulseekJobState.Succeeded:
                    await store.MarkSucceededAsync(entry.Id.Value, snapshot.ResultCount, cancellationToken);
                    break;
                case SoulseekJobState.Cancelled:
                    await store.MarkCancelledAsync(entry.Id.Value, cancellationToken);
                    break;
                default:
                    await store.MarkFailedAsync(entry.Id.Value, "job_failed", cancellationToken);
                    break;
            }
        }

        return activeCount;
    }

    private async Task StartClaimedAsync(
        ISoulseekWishlistStore store,
        SoulseekWishlistEntry entry,
        CancellationToken cancellationToken)
    {
        SearchHandle? handle = null;
        try
        {
            handle = await gateway.StartWishlistSearchAsync(
                new WishlistSearchRequest(entry.Query),
                cancellationToken);
            await store.MarkSubmittedAsync(entry.Id.Value, handle.WorkflowId, handle.EngineJobId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (handle != null)
                await TryCancelJobAsync(handle.EngineJobId, CancellationToken.None);
            logger.LogWarning(
                "Wishlist search submission failed with {ExceptionType}.",
                ex.GetType().Name);
            await store.MarkFailedAsync(entry.Id.Value, "submission_failed", cancellationToken);
        }
    }

    private async Task TryCancelJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            await gateway.CancelJobAsync(jobId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Wishlist job cancellation failed with {ExceptionType}.",
                ex.GetType().Name);
        }
    }

    private Task EnsureMigratedAsync(CancellationToken cancellationToken)
        => shouldMigrate && databaseMigration != null
            ? databaseMigration.EnsureMigratedAsync(cancellationToken)
            : Task.CompletedTask;

    private static ISoulseekWishlistStore GetStore(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<ISoulseekWishlistStore>();

    public override void Dispose()
    {
        operationLock.Dispose();
        base.Dispose();
    }
}
