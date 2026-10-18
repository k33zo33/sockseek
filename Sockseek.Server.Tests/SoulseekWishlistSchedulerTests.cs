using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Common;
using Sockseek.Application.Soulseek;
using Sockseek.Domain.Soulseek;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class SoulseekWishlistSchedulerTests
{
    [TestMethod]
    public async Task ProcessOnceAsync_StartsDueEntryOnceAndTracksActiveJob()
    {
        await using var fixture = await SchedulerFixture.CreateAsync();
        var handle = fixture.Gateway.EnqueueHandle();
        var entry = await fixture.Scheduler.CreateAsync("artist title", 30, enabled: true);

        await fixture.Scheduler.ProcessOnceAsync();

        Assert.AreEqual(1, fixture.Gateway.Requests.Count);
        Assert.AreEqual("artist title", fixture.Gateway.Requests.Single().Query);
        var searching = await fixture.FindAsync(entry.Id.Value);
        Assert.IsNotNull(searching);
        Assert.AreEqual(WishlistSearchState.Searching, searching.SearchState);
        Assert.AreEqual(handle.EngineJobId, searching.ActiveJobId);

        fixture.Gateway.SetJob(new JobSnapshot(
            handle.EngineJobId,
            handle.WorkflowId,
            SoulseekJobKind.TrackSearch,
            SoulseekJobState.Running,
            "wishlist"));
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(1);
        await fixture.Scheduler.ProcessOnceAsync();

        Assert.AreEqual(1, fixture.Gateway.Requests.Count);
    }

    [TestMethod]
    public async Task ProcessOnceAsync_CompletedJobPersistsBoundedResultSummary()
    {
        await using var fixture = await SchedulerFixture.CreateAsync();
        var handle = fixture.Gateway.EnqueueHandle();
        var entry = await fixture.Scheduler.CreateAsync("rare track", 30, enabled: true);
        await fixture.Scheduler.ProcessOnceAsync();

        fixture.Gateway.SetJob(new JobSnapshot(
            handle.EngineJobId,
            handle.WorkflowId,
            SoulseekJobKind.TrackSearch,
            SoulseekJobState.Succeeded,
            "rare track",
            ResultCount: 12));
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(1);
        await fixture.Scheduler.ProcessOnceAsync();

        var completed = await fixture.FindAsync(entry.Id.Value);
        Assert.IsNotNull(completed);
        Assert.AreEqual(WishlistSearchState.Succeeded, completed.SearchState);
        Assert.AreEqual(12, completed.LastResultCount);
        Assert.IsNull(completed.ActiveJobId);
        Assert.AreEqual(1, fixture.Gateway.Requests.Count);
    }

    [TestMethod]
    public async Task ProcessOnceAsync_SubmissionFailurePersistsOnlyStableErrorCode()
    {
        await using var fixture = await SchedulerFixture.CreateAsync();
        fixture.Gateway.SubmissionException = new InvalidOperationException("password=hunter2");
        var entry = await fixture.Scheduler.CreateAsync("query", 30, enabled: true);

        await fixture.Scheduler.ProcessOnceAsync();

        var failed = await fixture.FindAsync(entry.Id.Value);
        Assert.IsNotNull(failed);
        Assert.AreEqual(WishlistSearchState.Failed, failed.SearchState);
        Assert.AreEqual("submission_failed", failed.LastErrorCode);
        Assert.IsNotNull(failed.LastErrorCode);
        Assert.IsFalse(failed.LastErrorCode!.Contains("hunter2", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ProcessOnceAsync_EnforcesGlobalConcurrentSearchBound()
    {
        await using var fixture = await SchedulerFixture.CreateAsync(maximumConcurrentSearches: 4);
        for (int index = 0; index < 5; index++)
        {
            fixture.Gateway.EnqueueHandle();
            await fixture.Scheduler.CreateAsync($"query {index}", 30, enabled: true);
        }

        await fixture.Scheduler.ProcessOnceAsync();

        Assert.AreEqual(4, fixture.Gateway.Requests.Count);
        Assert.AreEqual(4, (await fixture.ListAsync()).Count(entry => entry.SearchState == WishlistSearchState.Searching));
        Assert.AreEqual(1, (await fixture.ListAsync()).Count(entry => entry.SearchState == WishlistSearchState.NeverRun));
    }

    [TestMethod]
    public async Task UpdateAsync_DisablingActiveEntryCancelsItsEngineJob()
    {
        await using var fixture = await SchedulerFixture.CreateAsync();
        var handle = fixture.Gateway.EnqueueHandle();
        var entry = await fixture.Scheduler.CreateAsync("query", 30, enabled: true);
        await fixture.Scheduler.ProcessOnceAsync();

        var updated = await fixture.Scheduler.UpdateAsync(entry.Id.Value, "query", 30, enabled: false);

        Assert.IsNotNull(updated);
        Assert.IsFalse(updated.Enabled);
        Assert.AreEqual(WishlistSearchState.Cancelled, updated.SearchState);
        CollectionAssert.AreEqual(new[] { handle.EngineJobId }, fixture.Gateway.CancelledJobIds);
    }

    [TestMethod]
    public async Task UpdateAsync_ChangingActiveQueryCancelsOldJobAndMakesReplacementDue()
    {
        await using var fixture = await SchedulerFixture.CreateAsync();
        var handle = fixture.Gateway.EnqueueHandle();
        var entry = await fixture.Scheduler.CreateAsync("query", 30, enabled: true);
        await fixture.Scheduler.ProcessOnceAsync();

        var updated = await fixture.Scheduler.UpdateAsync(entry.Id.Value, "replacement", 60, enabled: true);

        Assert.IsNotNull(updated);
        Assert.AreEqual(WishlistSearchState.Cancelled, updated.SearchState);
        Assert.AreEqual(fixture.Clock.UtcNow, updated.NextSearchAtUtc);
        CollectionAssert.AreEqual(new[] { handle.EngineJobId }, fixture.Gateway.CancelledJobIds);
    }

    [TestMethod]
    public async Task RunNowAsync_RefusesToExceedGlobalSearchBound()
    {
        await using var fixture = await SchedulerFixture.CreateAsync(maximumConcurrentSearches: 1);
        fixture.Gateway.EnqueueHandle();
        await fixture.Scheduler.CreateAsync("active", 30, enabled: true);
        var waiting = await fixture.Scheduler.CreateAsync("waiting", 30, enabled: false);
        await fixture.Scheduler.ProcessOnceAsync();
        await fixture.Scheduler.UpdateAsync(waiting.Id.Value, "waiting", 30, enabled: true);

        var result = await fixture.Scheduler.RunNowAsync(waiting.Id.Value);

        Assert.IsTrue(result.Found);
        Assert.IsFalse(result.Started);
        Assert.AreEqual(1, fixture.Gateway.Requests.Count);
    }

    [TestMethod]
    public async Task RunNowAsync_StartsEnabledEntryBeforeItsNextScheduledTime()
    {
        await using var fixture = await SchedulerFixture.CreateAsync();
        var firstHandle = fixture.Gateway.EnqueueHandle();
        var entry = await fixture.Scheduler.CreateAsync("query", 30, enabled: true);
        await fixture.Scheduler.ProcessOnceAsync();
        fixture.Gateway.SetJob(new JobSnapshot(
            firstHandle.EngineJobId,
            firstHandle.WorkflowId,
            SoulseekJobKind.TrackSearch,
            SoulseekJobState.Succeeded,
            "query",
            ResultCount: 1));
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(1);
        await fixture.Scheduler.ProcessOnceAsync();
        var secondHandle = fixture.Gateway.EnqueueHandle();

        var result = await fixture.Scheduler.RunNowAsync(entry.Id.Value);

        Assert.IsTrue(result.Found);
        Assert.IsTrue(result.Started);
        Assert.AreEqual(secondHandle.EngineJobId, result.Entry?.ActiveJobId);
        Assert.AreEqual(2, fixture.Gateway.Requests.Count);
    }

    private sealed class SchedulerFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ServiceProvider services;

        private SchedulerFixture(
            SqliteConnection connection,
            ServiceProvider services,
            FakeClock clock,
            FakeGateway gateway,
            SoulseekWishlistScheduler scheduler)
        {
            this.connection = connection;
            this.services = services;
            Clock = clock;
            Gateway = gateway;
            Scheduler = scheduler;
        }

        public FakeClock Clock { get; }
        public FakeGateway Gateway { get; }
        public SoulseekWishlistScheduler Scheduler { get; }

        public static async Task<SchedulerFixture> CreateAsync(int maximumConcurrentSearches = 4)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var clock = new FakeClock(new DateTimeOffset(2026, 10, 18, 13, 0, 0, TimeSpan.Zero));
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IClock>(clock);
            serviceCollection.AddDbContext<SockseekDbContext>(options => options.UseSqlite(connection));
            serviceCollection.AddScoped<ISoulseekWishlistStore, SoulseekWishlistStore>();
            var services = serviceCollection.BuildServiceProvider();

            using (var scope = services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<SockseekDbContext>().Database.EnsureCreatedAsync();

            var gateway = new FakeGateway();
            var scheduler = new SoulseekWishlistScheduler(
                services.GetRequiredService<IServiceScopeFactory>(),
                gateway,
                clock,
                NullLogger<SoulseekWishlistScheduler>.Instance,
                maximumConcurrentSearches);
            return new SchedulerFixture(connection, services, clock, gateway, scheduler);
        }

        public async Task<SoulseekWishlistEntry?> FindAsync(Guid id)
        {
            using var scope = services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISoulseekWishlistStore>().FindAsync(id);
        }

        public async Task<IReadOnlyList<SoulseekWishlistEntry>> ListAsync()
        {
            using var scope = services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISoulseekWishlistStore>().ListAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Scheduler.Dispose();
            await services.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class FakeGateway : ISoulseekEngineGateway
    {
        private readonly Queue<SearchHandle> handles = new();
        private readonly Dictionary<Guid, JobSnapshot> jobs = [];

        public List<WishlistSearchRequest> Requests { get; } = [];
        public List<Guid> CancelledJobIds { get; } = [];
        public Exception? SubmissionException { get; set; }

        public SearchHandle EnqueueHandle()
        {
            var handle = new SearchHandle(Guid.NewGuid(), Guid.NewGuid());
            handles.Enqueue(handle);
            return handle;
        }

        public void SetJob(JobSnapshot snapshot) => jobs[snapshot.EngineJobId] = snapshot;

        public Task<SearchHandle> StartWishlistSearchAsync(WishlistSearchRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return SubmissionException == null
                ? Task.FromResult(handles.Dequeue())
                : Task.FromException<SearchHandle>(SubmissionException);
        }

        public Task<SearchHandle> StartTrackSearchAsync(TrackSearchRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SearchHandle> StartAlbumSearchAsync(AlbumSearchRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<DownloadHandle> StartTrackDownloadAsync(TrackSearchRequest request, DownloadOptions options, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<DownloadHandle> StartDownloadAsync(CandidateReference candidate, DownloadOptions options, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task CancelJobAsync(Guid engineJobId, CancellationToken cancellationToken)
        {
            CancelledJobIds.Add(engineJobId);
            return Task.CompletedTask;
        }

        public Task<bool> TryNextCandidateAsync(Guid engineJobId, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task<JobSnapshot?> GetJobAsync(Guid engineJobId, CancellationToken cancellationToken)
            => Task.FromResult<JobSnapshot?>(jobs.GetValueOrDefault(engineJobId));

        public Task<DownloadJobResultSnapshot?> GetDownloadResultAsync(Guid engineJobId, CancellationToken cancellationToken)
            => Task.FromResult<DownloadJobResultSnapshot?>(null);

        public async IAsyncEnumerable<EngineEventEnvelope> SubscribeAsync(
            Guid workflowId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
