using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Core;
using Sockseek.Core.Jobs;
using Sockseek.Core.Models;
using Sockseek.Core.Settings;
using Sockseek.Api;
using Sockseek.Server;
using Soulseek;

namespace Tests.Server;

[TestClass]
public class EngineStateStoreTests
{
    [TestMethod]
    public void SongPayload_IncludesSnapshotProgress()
    {
        var store = new EngineStateStore();
        var song = new SongJob(new SongQuery { Artist = "Artist", Title = "Track" })
        {
            BytesTransferred = 25,
            FileSize = 100,
        };
        song.UpdateActivity(JobActivityPhase.Downloading);

        Register(store, song);

        var payload = store.GetJobDetail(song.Id)?.Payload as SongJobPayloadDto;
        Assert.IsNotNull(payload);
        Assert.AreEqual(25, payload.BytesTransferred);
        Assert.AreEqual(100, payload.TotalBytes);
        Assert.AreEqual(25d, payload.ProgressPercent);
    }

    [TestMethod]
    public void SongPayload_IncludesDownloadSource()
    {
        var store = new EngineStateStore();
        var song = new SongJob(new SongQuery { Artist = "Artist", Title = "Track" });
        song.SetDone("C:/music/track.mp3", downloadSource: SongDownloadSource.Fallback);

        Register(store, song);

        var payload = store.GetJobDetail(song.Id)?.Payload as SongJobPayloadDto;
        Assert.IsNotNull(payload);
        Assert.AreEqual(ServerSongDownloadSource.Fallback, payload.DownloadSource);
    }

    [TestMethod]
    public void GetActiveProgressiveDownloadSnapshot_ActiveSoulseekDownload_ReturnsIncompleteMediaSource()
    {
        var store = new EngineStateStore();
        var tempDir = System.IO.Directory.CreateTempSubdirectory("sockseek-progressive-");
        var finalPath = Path.Combine(tempDir.FullName, "track.mp3");
        var incompletePath = finalPath + ".incomplete";

        try
        {
            System.IO.File.WriteAllBytes(incompletePath, new byte[1234]);
            var song = ActiveDownloadSong(finalPath);
            Register(store, song);

            var snapshot = store.GetActiveProgressiveDownloadSnapshot(song.Id, progressivePlaybackEnabled: true);

            Assert.IsNotNull(snapshot);
            Assert.AreEqual(song.Id, snapshot.JobId);
            Assert.AreEqual(song.WorkflowId, snapshot.WorkflowId);
            Assert.AreEqual(incompletePath, snapshot.Source.Path);
            Assert.AreEqual(finalPath, snapshot.Source.FinalPath);
            Assert.AreEqual(".mp3", snapshot.Source.CodecExtension);
            Assert.AreEqual(960_000, snapshot.Source.ExpectedBytes);
            Assert.AreEqual(128, snapshot.Source.BitrateKbps);
            Assert.AreEqual(TimeSpan.FromSeconds(60), snapshot.Source.Duration);
            Assert.IsTrue(snapshot.Source.ProgressivePlaybackEnabled);
            Assert.AreEqual(1234, snapshot.Buffer.AvailableBytes);
            Assert.AreEqual(960_000, snapshot.Buffer.ExpectedBytes);
            Assert.IsFalse(snapshot.Buffer.DownloadCompleted);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void GetActiveProgressiveDownloadSnapshot_NoIncompleteExtension_ReturnsNull()
    {
        var store = new EngineStateStore();
        var song = ActiveDownloadSong(@"C:\music\track.mp3");
        song.Config = new DownloadSettings
        {
            Transfer = new TransferSettings { NoIncompleteExt = true },
        };
        Register(store, song);

        var snapshot = store.GetActiveProgressiveDownloadSnapshot(song.Id, progressivePlaybackEnabled: true);

        Assert.IsNull(snapshot);
    }

    [TestMethod]
    public void GetActiveProgressiveDownloadSnapshot_TerminalSong_ReturnsNull()
    {
        var store = new EngineStateStore();
        var song = ActiveDownloadSong(@"C:\music\track.mp3");
        song.SetDone(@"C:\music\track.mp3", song.ResolvedTarget);
        Register(store, song);

        var snapshot = store.GetActiveProgressiveDownloadSnapshot(song.Id, progressivePlaybackEnabled: true);

        Assert.IsNull(snapshot);
    }

    [TestMethod]
    public void GetActiveProgressiveDownloadSnapshot_ProgressEventsEstimateDownloadSpeed()
    {
        var now = DateTimeOffset.Parse("2026-09-22T12:00:00Z");
        var store = new EngineStateStore(() => now);
        var song = ActiveDownloadSong(@"C:\music\track.mp3");
        Register(store, song);

        DownloadProgress(store, song, 1000, 960_000);
        now = now.AddSeconds(2);
        DownloadProgress(store, song, 3000, 960_000);

        var snapshot = store.GetActiveProgressiveDownloadSnapshot(song.Id, progressivePlaybackEnabled: true);

        Assert.IsNotNull(snapshot);
        Assert.AreEqual(1000d, snapshot.Buffer.DownloadBytesPerSecond);
    }

    [TestMethod]
    public void GetActiveProgressiveDownloadSnapshot_ProgressEventTotalFillsUnknownExpectedBytes()
    {
        var store = new EngineStateStore();
        var song = ActiveDownloadSong(@"C:\music\track.mp3", size: 0);
        Register(store, song);
        DownloadProgress(store, song, 2048, 4096);

        var snapshot = store.GetActiveProgressiveDownloadSnapshot(song.Id, progressivePlaybackEnabled: true);

        Assert.IsNotNull(snapshot);
        Assert.AreEqual(4096, snapshot.Source.ExpectedBytes);
        Assert.AreEqual(4096, snapshot.Buffer.ExpectedBytes);
        Assert.AreEqual(2048, snapshot.Buffer.AvailableBytes);
    }

    [TestMethod]
    public void JobSummary_ExposesLifecycleActivityAndTerminalOutcome()
    {
        var store = new EngineStateStore();
        var until = DateTimeOffset.UtcNow.AddSeconds(30);
        var song = new SongJob(new SongQuery { Artist = "Artist", Title = "Track" });
        song.UpdateActivity(JobActivityPhase.SearchRateLimited, until);

        Register(store, song);

        var summary = store.GetJobSummary(song.Id);
        Assert.IsNotNull(summary);
        Assert.AreEqual(ServerJobLifecycleState.Running, summary.LifecycleState);
        Assert.AreEqual(ServerJobActivityPhase.SearchRateLimited, summary.ActivityPhase);
        Assert.AreEqual(until, summary.ActivityUntilUtc);
        Assert.AreEqual(ServerJobTerminalOutcome.None, summary.TerminalOutcome);
    }

    [TestMethod]
    public void JobDiscoveryChanged_UpdatesSummaryDiscoveryCounts()
    {
        var store = new EngineStateStore();
        var album = new AlbumJob(new AlbumQuery { Artist = "Artist", Album = "Album" });
        JobSummaryDto? published = null;
        store.JobUpserted += summary => published = summary;

        Register(store, album);

        album.Discovery = new DiscoverySummary { RawResultCount = 123, LockedFileCount = 4 };
        DiscoveryChanged(store, album);

        var summary = store.GetJobSummary(album.Id);
        Assert.IsNotNull(summary);
        Assert.AreEqual(123, summary.DiscoveryRawResultCount);
        Assert.AreEqual(4, summary.DiscoveryLockedFileCount);
        Assert.IsNotNull(published);
        Assert.AreEqual(123, published.DiscoveryRawResultCount);
        Assert.AreEqual(4, published.DiscoveryLockedFileCount);
    }

    [TestMethod]
    public void GetJobs_FiltersByLifecycleAndTerminalOutcome()
    {
        var store = new EngineStateStore();
        var running = new SongJob(new SongQuery { Title = "Running" });
        var done = new SongJob(new SongQuery { Title = "Done" });
        var failed = new SongJob(new SongQuery { Title = "Failed" });
        running.UpdateActivity(JobActivityPhase.Downloading);
        done.SetDone();
        failed.Fail(JobFailureReason.Other);

        Register(store, running);
        Register(store, done);
        Register(store, failed);

        var runningJobs = store.GetJobs(new JobQuery(
            ServerJobLifecycleState.Running,
            TerminalOutcome: null,
            Kind: null,
            WorkflowId: null,
            IncludeAll: true));
        CollectionAssert.AreEquivalent(new[] { running.Id }, runningJobs.Select(job => job.JobId).ToArray());

        var failedJobs = store.GetJobs(new JobQuery(
            ServerJobLifecycleState.Terminal,
            ServerJobTerminalOutcome.Failed,
            Kind: null,
            WorkflowId: null,
            IncludeAll: true));
        CollectionAssert.AreEquivalent(new[] { failed.Id }, failedJobs.Select(job => job.JobId).ToArray());
    }

    [TestMethod]
    public void AggregatePayload_IncludesSongOutcomeCounts()
    {
        var store = new EngineStateStore();
        var aggregate = new AggregateJob(new SongQuery { Artist = "Artist" });
        var s1 = new SongJob(new SongQuery { Title = "One" }); s1.SetDone();
        var s2 = new SongJob(new SongQuery { Title = "Two" }); s2.Fail(JobFailureReason.Other);
        var s3 = new SongJob(new SongQuery { Title = "Three" }); s3.UpdateActivity(JobActivityPhase.Downloading);
        aggregate.Songs.Add(s1);
        aggregate.Songs.Add(s2);
        aggregate.Songs.Add(s3);

        Register(store, aggregate);

        var payload = store.GetJobDetail(aggregate.Id)?.Payload as AggregateJobPayloadDto;
        Assert.IsNotNull(payload);
        Assert.AreEqual(3, payload.SongCount);
        Assert.AreEqual(2, payload.CompletedSongCount);
        Assert.AreEqual(1, payload.SucceededSongCount);
        Assert.AreEqual(1, payload.FailedSongCount);
    }

    [TestMethod]
    public void JobListPayload_IncludesDirectChildOutcomeCounts()
    {
        var store = new EngineStateStore();
        var list = new JobList("batch");
        var j1 = new SongJob(new SongQuery { Title = "One" }); j1.SetDone();
        var j2 = new SongJob(new SongQuery { Title = "Two" }); j2.Fail(JobFailureReason.Other);
        var j3 = new SongJob(new SongQuery { Title = "Three" }); j3.UpdateActivity(JobActivityPhase.Searching);
        list.Add(j1);
        list.Add(j2);
        list.Add(j3);

        Register(store, list);

        var payload = store.GetJobDetail(list.Id)?.Payload as JobListPayloadDto;
        Assert.IsNotNull(payload);
        Assert.AreEqual(3, payload.Count);
        Assert.AreEqual(1, payload.ActiveJobCount);
        Assert.AreEqual(2, payload.CompletedJobCount);
        Assert.AreEqual(1, payload.SucceededJobCount);
        Assert.AreEqual(1, payload.FailedJobCount);
    }

    [TestMethod]
    public void JobListSummary_UsesCoreRunningState()
    {
        var store = new EngineStateStore();
        var list = new JobList("batch");
        var child = new SongJob(new SongQuery { Title = "One" });
        list.Add(child);

        Register(store, list);
        Register(store, child, list);

        list.UpdateActivity(JobActivityPhase.RunningChildren);
        UpdateState(store, list);

        var summary = store.GetJobSummary(list.Id);
        Assert.IsNotNull(summary);
        Assert.AreEqual(ServerJobLifecycleState.Running, summary.LifecycleState);
        Assert.AreEqual(ServerJobActivityPhase.RunningChildren, summary.ActivityPhase);
        Assert.AreEqual(ServerJobTerminalOutcome.None, summary.TerminalOutcome);
    }

    [TestMethod]
    public void WorkflowSummary_TracksRootsTitleAndCountsAcrossJobUpdates()
    {
        var store = new EngineStateStore();
        var list = new JobList("batch");
        var done = new SongJob(new SongQuery { Title = "Done" }) { WorkflowId = list.WorkflowId };
        var failed = new SongJob(new SongQuery { Title = "Failed" }) { WorkflowId = list.WorkflowId };

        Register(store, list);
        Register(store, done, list);
        Register(store, failed, list);

        var initial = store.GetWorkflowSummary(list.WorkflowId);
        Assert.IsNotNull(initial);
        Assert.AreEqual("batch", initial.Title);
        CollectionAssert.AreEqual(new[] { list.Id }, initial.RootJobIds.ToArray());
        Assert.AreEqual(ServerWorkflowState.Active, initial.State);
        Assert.AreEqual(3, initial.ActiveJobCount);
        Assert.AreEqual(0, initial.CompletedJobCount);
        Assert.AreEqual(0, initial.FailedJobCount);

        done.SetDone();
        UpdateState(store, done);
        failed.Fail(JobFailureReason.Other);
        UpdateState(store, failed);
        list.SetDone();
        UpdateState(store, list);

        var terminal = store.GetWorkflowSummary(list.WorkflowId);
        Assert.IsNotNull(terminal);
        Assert.AreEqual(ServerWorkflowState.Failed, terminal.State);
        Assert.AreEqual(0, terminal.ActiveJobCount);
        Assert.AreEqual(3, terminal.CompletedJobCount);
        Assert.AreEqual(1, terminal.FailedJobCount);
    }

    [TestMethod]
    public void WorkflowSummaryCache_MatchesBruteForceSnapshotAcrossMutations()
    {
        var store = new EngineStateStore();
        var extract = new ExtractJob("input.csv", InputType.CSV)
        {
            AutoProcessResult = true,
        };
        var list = new JobList("batch") { WorkflowId = extract.WorkflowId };
        var done = new SongJob(new SongQuery { Title = "Done" }) { WorkflowId = extract.WorkflowId };
        var failed = new SongJob(new SongQuery { Title = "Failed" }) { WorkflowId = extract.WorkflowId };
        var projected = new SongJob(new SongQuery { Title = "Projected" }) { WorkflowId = extract.WorkflowId };
        list.Add(done);
        list.Add(failed);
        list.Add(projected);
        extract.Result = list;

        Register(store, extract);
        AssertWorkflowSummaryMatchesBruteForceSnapshot(store, extract.WorkflowId);

        ResultCreated(store, extract, list);
        AssertWorkflowSummaryMatchesBruteForceSnapshot(store, extract.WorkflowId);

        Register(store, list);
        Register(store, done, list);
        Register(store, failed, list);
        Register(store, projected, list);
        AssertWorkflowSummaryMatchesBruteForceSnapshot(store, extract.WorkflowId);

        store.SetSourceJob(done.Id, extract.Id);
        AssertWorkflowSummaryMatchesBruteForceSnapshot(store, extract.WorkflowId);

        done.SetDone();
        UpdateState(store, done);
        failed.Fail(JobFailureReason.Other);
        UpdateState(store, failed);
        ExecutionCompleted(store, projected);
        list.SetDone();
        UpdateState(store, list);
        extract.SetDone();
        UpdateState(store, extract);
        AssertWorkflowSummaryMatchesBruteForceSnapshot(store, extract.WorkflowId);
    }

    [TestMethod]
    public void CompletedWorkflowHistory_PrunesOldTerminalWorkflowsButKeepsActiveWorkflows()
    {
        var store = new EngineStateStore(maxCompletedWorkflowHistory: 2);
        var prunedWorkflowIds = new List<Guid>();
        store.WorkflowPruned += prunedWorkflowIds.Add;

        var first = RegisterCompletedWorkflow(store, "first");
        var second = RegisterCompletedWorkflow(store, "second");
        var third = RegisterCompletedWorkflow(store, "third");

        Assert.IsNull(store.GetWorkflowSummary(first.WorkflowId));
        Assert.IsNull(store.GetJobSummary(first.Id));
        CollectionAssert.Contains(prunedWorkflowIds, first.WorkflowId);
        Assert.IsNotNull(store.GetWorkflowSummary(second.WorkflowId));
        Assert.IsNotNull(store.GetWorkflowSummary(third.WorkflowId));
        Assert.AreEqual(2, store.GetWorkflows().Count);

        var active = new SongJob(new SongQuery { Title = "active" });
        Register(store, active);

        var fourth = RegisterCompletedWorkflow(store, "fourth");

        Assert.IsNull(store.GetWorkflowSummary(second.WorkflowId));
        CollectionAssert.Contains(prunedWorkflowIds, second.WorkflowId);
        Assert.IsNotNull(store.GetWorkflowSummary(third.WorkflowId));
        Assert.IsNotNull(store.GetWorkflowSummary(fourth.WorkflowId));
        Assert.IsNotNull(store.GetWorkflowSummary(active.WorkflowId));
        Assert.AreEqual(3, store.GetWorkflows().Count);
        Assert.AreEqual(1, store.GetStatistics().ActiveWorkflowCount);
    }

    [TestMethod]
    public void AlbumAggregatePayload_CountsProducedAlbumDescendants()
    {
        var store = new EngineStateStore();
        var aggregate = new AlbumAggregateJob(new AlbumQuery { Artist = "Artist" });
        var list = new JobList("albums");
        var firstAlbum = new AlbumJob(new AlbumQuery { Artist = "Artist", Album = "One" });
        var secondAlbum = new AlbumJob(new AlbumQuery { Artist = "Artist", Album = "Two" });
        list.Add(firstAlbum);
        list.Add(secondAlbum);

        Register(store, aggregate);
        Register(store, list, aggregate);
        Register(store, firstAlbum, list);
        Register(store, secondAlbum, list);

        var payload = store.GetJobDetail(aggregate.Id)?.Payload as AlbumAggregateJobPayloadDto;
        Assert.IsNotNull(payload);
        Assert.AreEqual(2, payload.ResultCount);
    }

    [TestMethod]
    public void AlbumDetail_TracksReflectCurrentTransferState()
    {
        var store = new EngineStateStore();
        var album = new AlbumJob(new AlbumQuery { Artist = "Artist", Album = "Album" });
        var song1 = new SongJob(new SongQuery { Title = "One" });
        var song2 = new SongJob(new SongQuery { Title = "Two" });
        song1.UpdateActivity(JobActivityPhase.Downloading);
        song2.UpdateActivity(JobActivityPhase.Downloading);

        Register(store, album);
        Register(store, song1, album);
        Register(store, song2, album);

        // Fire transfer state after registration — the cached record payload won't have it
        DownloadStateChanged(store, song1, TransferStates.InProgress);
        DownloadStateChanged(store, song2, TransferStates.Queued | TransferStates.Remotely);

        var tracks = (store.GetJobDetail(album.Id)?.Payload as AlbumJobPayloadDto)?.Tracks;
        Assert.IsNotNull(tracks);
        Assert.AreEqual(2, tracks.Count);
        Assert.AreEqual(TransferStates.InProgress.ToString(), tracks[0].TransferState);
        Assert.AreEqual((TransferStates.Queued | TransferStates.Remotely).ToString(), tracks[1].TransferState);
    }


    [TestMethod]
    public void ResultDraft_RoundTripsSourceMutationProvenance()
    {
        var store = new EngineStateStore();
        var album = new AlbumJob(new AlbumQuery { Artist = "Artist", Album = "Album" })
        {
            ItemNumber = 2,
            LineNumber = 4,
            SourceMutation = SourceMutation.ClearCsvRow("input.csv", 4, 2, 3),
        };
        var extract = new ExtractJob("input.csv", InputType.CSV)
        {
            AutoProcessResult = false,
            Result = album,
        };

        Register(store, extract);

        var payload = store.GetJobDetail(extract.Id)?.Payload as ExtractJobPayloadDto;
        Assert.IsNotNull(payload);
        var draft = payload.ResultDraft as AlbumJobDraftDto;
        Assert.IsNotNull(draft);
        Assert.IsNotNull(draft.Provenance);
        Assert.AreEqual(2, draft.Provenance.ItemNumber);
        Assert.AreEqual(4, draft.Provenance.LineNumber);
        Assert.AreEqual(nameof(SourceMutationKind.ClearCsvRow), draft.Provenance.SourceMutation?.Kind);
        Assert.AreEqual("input.csv", draft.Provenance.SourceMutation?.Source);
        Assert.AreEqual(3, draft.Provenance.SourceMutation?.CsvColumnCount);

        var roundTripped = JobRequestMapper.CreateJob(draft);
        Assert.AreEqual(2, roundTripped.ItemNumber);
        Assert.AreEqual(4, roundTripped.LineNumber);
        Assert.AreEqual(SourceMutationKind.ClearCsvRow, roundTripped.SourceMutation?.Kind);
        Assert.AreEqual("input.csv", roundTripped.SourceMutation?.Source);
        Assert.AreEqual(3, roundTripped.SourceMutation?.CsvColumnCount);
    }

    [TestMethod]
    public void AutoProcessedExtractPayload_DoesNotInlineResultDraft()
    {
        var store = new EngineStateStore();
        var list = new JobList("batch");
        var extract = new ExtractJob("input.csv", InputType.CSV)
        {
            AutoProcessResult = true,
            Result = list,
        };
        list.WorkflowId = extract.WorkflowId;
        list.Add(new SongJob(new SongQuery { Artist = "Artist", Title = "One" }) { WorkflowId = list.WorkflowId });

        Register(store, extract);

        var payload = store.GetJobDetail(extract.Id)?.Payload as ExtractJobPayloadDto;
        Assert.IsNotNull(payload);
        Assert.AreEqual(list.Id, payload.ResultJobId);
        Assert.IsNull(payload.ResultDraft);
    }

    [TestMethod]
    public void AutoProcessedExtractResult_GetsDisplayIdBeforeRegistration()
    {
        var store = new EngineStateStore();
        var extract = new ExtractJob("input.csv", InputType.CSV)
        {
            AutoProcessResult = true,
        };
        var result = new JobList("batch") { WorkflowId = extract.WorkflowId };
        extract.Result = result;

        Register(store, extract);
        ResultCreated(store, extract, result);

        var resultSummary = store.GetJobSummary(result.Id);
        Assert.IsNotNull(resultSummary);
        Assert.AreNotEqual(0, resultSummary.DisplayId);
    }

    private static void Register(EngineStateStore store, Job job, Job? parent = null)
    {
        typeof(EngineStateStore)
            .GetMethod("OnJobRegistered", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(store, [job, parent]);
    }

    private static SongJob RegisterCompletedWorkflow(EngineStateStore store, string title)
    {
        var song = new SongJob(new SongQuery { Title = title });
        Register(store, song);
        song.SetDone();
        UpdateState(store, song);
        return song;
    }

    private static void UpdateState(EngineStateStore store, Job job)
    {
        typeof(EngineStateStore)
            .GetMethod("OnJobStateChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(store, [job]);
    }

    private static void DiscoveryChanged(EngineStateStore store, Job job)
    {
        typeof(EngineStateStore)
            .GetMethod("OnJobDiscoveryChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(store, [job]);
    }

    private static void ResultCreated(EngineStateStore store, ExtractJob job, Job result)
    {
        typeof(EngineStateStore)
            .GetMethod("OnJobResultCreated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(store, [job, result]);
    }

    private static void ExecutionCompleted(EngineStateStore store, Job job)
    {
        typeof(EngineStateStore)
            .GetMethod("OnJobExecutionCompleted", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(store, [job]);
    }

    private static void DownloadStateChanged(EngineStateStore store, SongJob song, TransferStates state)
    {
        typeof(EngineStateStore)
            .GetMethod("OnDownloadStateChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(store, [song, state]);
    }

    private static void DownloadProgress(EngineStateStore store, SongJob song, long bytesTransferred, long totalBytes)
    {
        typeof(EngineStateStore)
            .GetMethod("OnDownloadProgress", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(store, [song, bytesTransferred, totalBytes]);
    }

    private static SongJob ActiveDownloadSong(string downloadPath, long size = 960_000)
    {
        var response = new SearchResponse("peer", 1, true, 256, 0, []);
        var file = new Soulseek.File(
            1,
            @"remote\Artist\Album\01. Track.mp3",
            size,
            ".mp3",
            attributeList:
            [
                new FileAttribute(FileAttributeType.BitRate, 128),
                new FileAttribute(FileAttributeType.Length, 60),
            ]);

        var song = new SongJob(new SongQuery { Artist = "Artist", Title = "Track", Length = 60 })
        {
            Config = new DownloadSettings(),
            DownloadPath = downloadPath,
            ResolvedTarget = new FileCandidate(response, file),
            FileSize = size,
            BytesTransferred = 512,
        };
        song.UpdateActivity(JobActivityPhase.Downloading);
        return song;
    }

    private static void AssertWorkflowSummaryMatchesBruteForceSnapshot(EngineStateStore store, Guid workflowId)
    {
        var cached = store.GetWorkflowSummary(workflowId);
        var detail = store.GetWorkflow(workflowId, includeAll: true);

        Assert.IsNotNull(cached);
        Assert.IsNotNull(detail);

        var expected = BuildBruteForceWorkflowSummary(workflowId, detail.Jobs);
        Assert.AreEqual(expected.WorkflowId, cached.WorkflowId);
        Assert.AreEqual(expected.Title, cached.Title);
        Assert.AreEqual(expected.State, cached.State);
        CollectionAssert.AreEqual(expected.RootJobIds.ToArray(), cached.RootJobIds.ToArray());
        Assert.AreEqual(expected.ActiveJobCount, cached.ActiveJobCount);
        Assert.AreEqual(expected.FailedJobCount, cached.FailedJobCount);
        Assert.AreEqual(expected.CompletedJobCount, cached.CompletedJobCount);
    }

    private static WorkflowSummaryDto BuildBruteForceWorkflowSummary(Guid workflowId, IReadOnlyList<JobSummaryDto> jobs)
    {
        var ordered = jobs.OrderBy(job => job.DisplayId).ToList();
        string title = ordered.FirstOrDefault(job => !string.IsNullOrWhiteSpace(job.ItemName))?.ItemName
            ?? ordered.First().QueryText
            ?? ordered.First().Kind.ToWireString();

        int active = ordered.Count(job => job.LifecycleState != ServerJobLifecycleState.Terminal);
        int failed = ordered.Count(IsFailed);
        int completed = ordered.Count - active;
        var state = active > 0 ? ServerWorkflowState.Active
            : failed > 0 ? ServerWorkflowState.Failed
            : ServerWorkflowState.Completed;

        return new WorkflowSummaryDto(
            workflowId,
            title,
            state,
            ordered.Where(job => job.ParentJobId == null).Select(job => job.JobId).ToList(),
            active,
            failed,
            completed);
    }

    private static bool IsFailed(JobSummaryDto job)
        => job.TerminalOutcome is ServerJobTerminalOutcome.Failed
            or ServerJobTerminalOutcome.Cancelled
            or ServerJobTerminalOutcome.PartialSuccess
            || (job.TerminalOutcome == ServerJobTerminalOutcome.Skipped
                && job.SkipReason != ServerJobSkipReason.AlreadyExists);
}
