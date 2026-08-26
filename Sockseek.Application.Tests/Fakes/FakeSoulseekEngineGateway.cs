using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sockseek.Application.Soulseek;

namespace Tests.Application.Fakes;

internal sealed class FakeSoulseekEngineGateway : ISoulseekEngineGateway
{
    private readonly ConcurrentDictionary<Guid, JobSnapshot?> jobs = new();
    private readonly ConcurrentDictionary<Guid, List<EngineEventEnvelope>> eventsByWorkflow = new();
    private readonly Queue<SearchHandle> nextTrackSearchHandles = new();
    private readonly Queue<SearchHandle> nextAlbumSearchHandles = new();
    private readonly Queue<DownloadHandle> nextDownloadHandles = new();
    private readonly ConcurrentDictionary<Guid, bool> nextCandidateResults = new();

    public List<TrackSearchRequest> TrackSearchRequests { get; } = [];
    public List<AlbumSearchRequest> AlbumSearchRequests { get; } = [];
    public List<(CandidateReference Candidate, DownloadOptions Options)> DownloadRequests { get; } = [];
    public List<Guid> CancelledJobIds { get; } = [];
    public List<Guid> NextCandidateJobIds { get; } = [];

    public void EnqueueTrackSearchHandle(SearchHandle handle) => nextTrackSearchHandles.Enqueue(handle);
    public void EnqueueAlbumSearchHandle(SearchHandle handle) => nextAlbumSearchHandles.Enqueue(handle);
    public void EnqueueDownloadHandle(DownloadHandle handle) => nextDownloadHandles.Enqueue(handle);
    public void SetJob(JobSnapshot snapshot) => jobs[snapshot.EngineJobId] = snapshot;
    public void SetNextCandidateResult(Guid engineJobId, bool result) => nextCandidateResults[engineJobId] = result;

    public void AddEvent(Guid workflowId, EngineEventEnvelope envelope)
    {
        var events = eventsByWorkflow.GetOrAdd(workflowId, _ => []);
        lock (events)
            events.Add(envelope);
    }

    public Task<SearchHandle> StartTrackSearchAsync(TrackSearchRequest request, CancellationToken cancellationToken)
    {
        TrackSearchRequests.Add(request);
        return Task.FromResult(nextTrackSearchHandles.Dequeue());
    }

    public Task<SearchHandle> StartAlbumSearchAsync(AlbumSearchRequest request, CancellationToken cancellationToken)
    {
        AlbumSearchRequests.Add(request);
        return Task.FromResult(nextAlbumSearchHandles.Dequeue());
    }

    public Task<DownloadHandle> StartDownloadAsync(CandidateReference candidate, DownloadOptions options, CancellationToken cancellationToken)
    {
        DownloadRequests.Add((candidate, options));
        return Task.FromResult(nextDownloadHandles.Dequeue());
    }

    public Task CancelJobAsync(Guid engineJobId, CancellationToken cancellationToken)
    {
        CancelledJobIds.Add(engineJobId);
        return Task.CompletedTask;
    }

    public Task<bool> TryNextCandidateAsync(Guid engineJobId, CancellationToken cancellationToken)
    {
        NextCandidateJobIds.Add(engineJobId);
        return Task.FromResult(nextCandidateResults.GetValueOrDefault(engineJobId));
    }

    public Task<JobSnapshot?> GetJobAsync(Guid engineJobId, CancellationToken cancellationToken)
        => Task.FromResult(jobs.GetValueOrDefault(engineJobId));

    public async IAsyncEnumerable<EngineEventEnvelope> SubscribeAsync(Guid workflowId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var events = eventsByWorkflow.GetValueOrDefault(workflowId) ?? [];
        List<EngineEventEnvelope> snapshot;
        lock (events)
            snapshot = [.. events];

        foreach (var envelope in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return envelope;
            await Task.Yield();
        }
    }
}
