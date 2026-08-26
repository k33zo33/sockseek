using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Soulseek;
using Tests.Application.Fakes;

namespace Tests.Application.Soulseek;

[TestClass]
public class FakeSoulseekEngineGatewayTests
{
    [TestMethod]
    public async Task FakeGateway_CapturesRequests_AndReturnsConfiguredHandles()
    {
        var gateway = new FakeSoulseekEngineGateway();
        var workflowId = Guid.NewGuid();
        var trackHandle = new SearchHandle(workflowId, Guid.NewGuid());
        var albumHandle = new SearchHandle(workflowId, Guid.NewGuid());
        var downloadHandle = new DownloadHandle(workflowId, Guid.NewGuid());
        gateway.EnqueueTrackSearchHandle(trackHandle);
        gateway.EnqueueAlbumSearchHandle(albumHandle);
        gateway.EnqueueDownloadHandle(downloadHandle);

        var trackRequest = new TrackSearchRequest("Artist", "Track", "Album", "flac");
        var albumRequest = new AlbumSearchRequest("Artist", "Album", "deluxe");
        var candidate = new CandidateReference(Guid.NewGuid(), "remote-user", "Artist/Album/01 - Track.flac");
        var options = new DownloadOptions("/music", "lossless");

        var returnedTrackHandle = await gateway.StartTrackSearchAsync(trackRequest, CancellationToken.None);
        var returnedAlbumHandle = await gateway.StartAlbumSearchAsync(albumRequest, CancellationToken.None);
        var returnedDownloadHandle = await gateway.StartDownloadAsync(candidate, options, CancellationToken.None);

        Assert.AreEqual(trackHandle, returnedTrackHandle);
        Assert.AreEqual(albumHandle, returnedAlbumHandle);
        Assert.AreEqual(downloadHandle, returnedDownloadHandle);
        CollectionAssert.AreEqual(new[] { trackRequest }, gateway.TrackSearchRequests);
        CollectionAssert.AreEqual(new[] { albumRequest }, gateway.AlbumSearchRequests);
        Assert.AreEqual(1, gateway.DownloadRequests.Count);
        Assert.AreEqual(candidate, gateway.DownloadRequests[0].Candidate);
        Assert.AreEqual(options, gateway.DownloadRequests[0].Options);
    }

    [TestMethod]
    public async Task FakeGateway_StoresJobSnapshots_AndControlActions()
    {
        var gateway = new FakeSoulseekEngineGateway();
        var snapshot = new JobSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SoulseekJobKind.Download,
            SoulseekJobState.Running,
            "Downloading track");
        gateway.SetJob(snapshot);
        gateway.SetNextCandidateResult(snapshot.EngineJobId, true);

        var stored = await gateway.GetJobAsync(snapshot.EngineJobId, CancellationToken.None);
        Assert.AreEqual(snapshot, stored);

        var nextCandidate = await gateway.TryNextCandidateAsync(snapshot.EngineJobId, CancellationToken.None);
        Assert.IsTrue(nextCandidate);
        CollectionAssert.AreEqual(new[] { snapshot.EngineJobId }, gateway.NextCandidateJobIds);

        await gateway.CancelJobAsync(snapshot.EngineJobId, CancellationToken.None);
        CollectionAssert.AreEqual(new[] { snapshot.EngineJobId }, gateway.CancelledJobIds);
    }

    [TestMethod]
    public async Task FakeGateway_ReplaysEvents_ForWorkflowSubscription()
    {
        var gateway = new FakeSoulseekEngineGateway();
        var workflowId = Guid.NewGuid();
        var eventOne = new EngineEventEnvelope(
            Guid.NewGuid(),
            "download.progress",
            DateTimeOffset.UtcNow,
            "corr-1",
            workflowId,
            Guid.NewGuid(),
            1,
            new JobSnapshot(Guid.NewGuid(), workflowId, SoulseekJobKind.Download, SoulseekJobState.Running, "50%"));
        var eventTwo = eventOne with
        {
            EventId = Guid.NewGuid(),
            Sequence = 2,
            CorrelationId = "corr-2",
        };

        gateway.AddEvent(workflowId, eventOne);
        gateway.AddEvent(workflowId, eventTwo);

        var events = new List<EngineEventEnvelope>();
        await foreach (var envelope in gateway.SubscribeAsync(workflowId, CancellationToken.None))
            events.Add(envelope);

        CollectionAssert.AreEqual(new[] { eventOne, eventTwo }, events);
    }
}
