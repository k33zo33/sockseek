using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Application.Soulseek;
using Sockseek.Core.Settings;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public class ServerSoulseekEngineGatewayTests
{
    [TestMethod]
    public async Task Gateway_StartTrackSearch_AndGetJob_ReturnMappedSnapshot()
    {
        string musicRoot = Path.Combine(Path.GetTempPath(), "Sockseek-gateway-test-" + Guid.NewGuid());
        string albumDir = Path.Combine(musicRoot, "Artist", "Album");
        string outputDir = Path.Combine(Path.GetTempPath(), "Sockseek-gateway-out-" + Guid.NewGuid());
        Directory.CreateDirectory(albumDir);
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(albumDir, "01. Artist - Track One.mp3"), "a");

        await using var app = BuildApp(musicRoot, outputDir);
        try
        {
            await app.StartAsync();
            var gateway = app.Services.GetRequiredService<ISoulseekEngineGateway>();

            var handle = await gateway.StartTrackSearchAsync(
                new TrackSearchRequest("Artist", "Track One", "Album", null),
                CancellationToken.None);

            var snapshot = await WaitForSnapshotAsync(gateway, handle.EngineJobId, SoulseekJobState.Succeeded);
            Assert.AreEqual(handle.WorkflowId, snapshot.WorkflowId);
            Assert.AreEqual(handle.EngineJobId, snapshot.EngineJobId);
            Assert.AreEqual(SoulseekJobKind.TrackSearch, snapshot.Kind);
            Assert.AreEqual(SoulseekJobState.Succeeded, snapshot.State);
        }
        finally
        {
            await app.StopAsync();
            Directory.Delete(musicRoot, recursive: true);
            Directory.Delete(outputDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Gateway_StartDownload_ReusesWorkflow_AndMapsDownloadSnapshot()
    {
        string musicRoot = Path.Combine(Path.GetTempPath(), "Sockseek-gateway-test-" + Guid.NewGuid());
        string albumDir = Path.Combine(musicRoot, "Artist", "Album");
        string outputDir = Path.Combine(Path.GetTempPath(), "Sockseek-gateway-out-" + Guid.NewGuid());
        Directory.CreateDirectory(albumDir);
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(albumDir, "01. Artist - Track One.mp3"), "a");

        await using var app = BuildApp(musicRoot, outputDir);
        try
        {
            await app.StartAsync();
            var gateway = app.Services.GetRequiredService<ISoulseekEngineGateway>();
            var supervisor = app.Services.GetRequiredService<EngineSupervisor>();

            var searchHandle = await gateway.StartTrackSearchAsync(
                new TrackSearchRequest("Artist", "Track One", "Album", null),
                CancellationToken.None);
            await WaitForSnapshotAsync(gateway, searchHandle.EngineJobId, SoulseekJobState.Succeeded);

            var files = supervisor.GetFileResults(searchHandle.EngineJobId);
            Assert.IsNotNull(files);
            Assert.AreEqual(1, files.Items.Count);

            var downloadHandle = await gateway.StartDownloadAsync(
                new CandidateReference(searchHandle.EngineJobId, files.Items[0].Ref.Username, files.Items[0].Ref.Filename),
                new DownloadOptions(outputDir, null),
                CancellationToken.None);

            Assert.AreEqual(searchHandle.WorkflowId, downloadHandle.WorkflowId);

            var downloadSnapshot = await WaitForSnapshotAsync(gateway, downloadHandle.EngineJobId, SoulseekJobState.Succeeded);
            Assert.AreEqual(SoulseekJobKind.Download, downloadSnapshot.Kind);
            Assert.AreEqual(SoulseekJobState.Succeeded, downloadSnapshot.State);

            var downloaded = Directory.GetFiles(outputDir, "*.mp3", SearchOption.AllDirectories);
            Assert.AreEqual(1, downloaded.Length);
        }
        finally
        {
            await app.StopAsync();
            Directory.Delete(musicRoot, recursive: true);
            Directory.Delete(outputDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Gateway_ControlOperations_MapSupervisorBehavior()
    {
        string musicRoot = Path.Combine(Path.GetTempPath(), "Sockseek-gateway-test-" + Guid.NewGuid());
        string albumDir = Path.Combine(musicRoot, "Artist", "Album");
        string outputDir = Path.Combine(Path.GetTempPath(), "Sockseek-gateway-out-" + Guid.NewGuid());
        Directory.CreateDirectory(albumDir);
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(albumDir, "01. Artist - Track One.mp3"), "a");

        await using var app = BuildApp(musicRoot, outputDir);
        try
        {
            await app.StartAsync();
            var gateway = app.Services.GetRequiredService<ISoulseekEngineGateway>();

            var searchHandle = await gateway.StartTrackSearchAsync(
                new TrackSearchRequest("Artist", "Track One", "Album", null),
                CancellationToken.None);
            await WaitForSnapshotAsync(gateway, searchHandle.EngineJobId, SoulseekJobState.Succeeded);

            var nextCandidate = await gateway.TryNextCandidateAsync(searchHandle.EngineJobId, CancellationToken.None);
            Assert.IsFalse(nextCandidate);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                gateway.CancelJobAsync(Guid.NewGuid(), CancellationToken.None));
        }
        finally
        {
            await app.StopAsync();
            Directory.Delete(musicRoot, recursive: true);
            Directory.Delete(outputDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Gateway_SubscribeAsync_EmitsWorkflowScopedMappedEvents()
    {
        string musicRoot = Path.Combine(Path.GetTempPath(), "Sockseek-gateway-test-" + Guid.NewGuid());
        string albumDir = Path.Combine(musicRoot, "Artist", "Album");
        string outputDir = Path.Combine(Path.GetTempPath(), "Sockseek-gateway-out-" + Guid.NewGuid());
        Directory.CreateDirectory(albumDir);
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(albumDir, "01. Artist - Track One.mp3"), new string('a', 128 * 1024));

        using var cts = new CancellationTokenSource();
        Task runTask = Task.CompletedTask;
        try
        {
            var supervisor = CreateSupervisor(musicRoot, outputDir, slowMockFiles: true);
            var broadcaster = new ServerEventBroadcaster(supervisor.StateStore, supervisor, new NoOpHubContext<ServerEventHub>());
            var gateway = new ServerSoulseekEngineGateway(supervisor, broadcaster);
            runTask = supervisor.RunAsync(cts.Token);

            var handle = await gateway.StartTrackSearchAsync(
                new TrackSearchRequest("Artist", "Track One", "Album", null),
                CancellationToken.None);
            await WaitForSnapshotAsync(gateway, handle.EngineJobId, SoulseekJobState.Succeeded);

            var files = supervisor.GetFileResults(handle.EngineJobId);
            Assert.IsNotNull(files);

            var eventTask = WaitForEnvelopeAsync(
                gateway,
                handle.WorkflowId,
                e => e.Snapshot?.Kind == SoulseekJobKind.Download && e.Snapshot.State is SoulseekJobState.Queued or SoulseekJobState.Running);

            var downloadHandle = await gateway.StartDownloadAsync(
                new CandidateReference(handle.EngineJobId, files.Items[0].Ref.Username, files.Items[0].Ref.Filename),
                new DownloadOptions(outputDir, null),
                CancellationToken.None);

            var envelope = await eventTask;
            Assert.AreEqual(handle.WorkflowId, envelope.WorkflowId);
            Assert.AreEqual(downloadHandle.EngineJobId, envelope.EntityId);
            Assert.AreEqual($"server-event-{envelope.Sequence}", envelope.CorrelationId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(envelope.EventType));
            Assert.IsNotNull(envelope.Snapshot);
        }
        finally
        {
            cts.Cancel();
            await runTask;
            Directory.Delete(musicRoot, recursive: true);
            Directory.Delete(outputDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task Gateway_SubscribeAsync_AllowsSnapshotRecoveryAfterResubscribe()
    {
        string musicRoot = Path.Combine(Path.GetTempPath(), "Sockseek-gateway-test-" + Guid.NewGuid());
        string albumDir = Path.Combine(musicRoot, "Artist", "Album");
        string outputDir = Path.Combine(Path.GetTempPath(), "Sockseek-gateway-out-" + Guid.NewGuid());
        Directory.CreateDirectory(albumDir);
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(albumDir, "01. Artist - Track One.mp3"), new string('a', 128 * 1024));

        using var cts = new CancellationTokenSource();
        Task runTask = Task.CompletedTask;
        try
        {
            var supervisor = CreateSupervisor(musicRoot, outputDir, slowMockFiles: true);
            var broadcaster = new ServerEventBroadcaster(supervisor.StateStore, supervisor, new NoOpHubContext<ServerEventHub>());
            var gateway = new ServerSoulseekEngineGateway(supervisor, broadcaster);
            runTask = supervisor.RunAsync(cts.Token);

            var searchHandle = await gateway.StartTrackSearchAsync(
                new TrackSearchRequest("Artist", "Track One", "Album", null),
                CancellationToken.None);
            await WaitForSnapshotAsync(gateway, searchHandle.EngineJobId, SoulseekJobState.Succeeded);

            var files = supervisor.GetFileResults(searchHandle.EngineJobId);
            Assert.IsNotNull(files);

            var firstSubscription = WaitForEnvelopeAsync(
                gateway,
                searchHandle.WorkflowId,
                e => e.Snapshot?.Kind == SoulseekJobKind.Download && e.Snapshot.State is SoulseekJobState.Queued or SoulseekJobState.Running);

            var downloadHandle = await gateway.StartDownloadAsync(
                new CandidateReference(searchHandle.EngineJobId, files.Items[0].Ref.Username, files.Items[0].Ref.Filename),
                new DownloadOptions(outputDir, null),
                CancellationToken.None);

            var firstEvent = await firstSubscription;
            var secondSubscription = WaitForEnvelopeAsync(
                gateway,
                searchHandle.WorkflowId,
                e => e.EntityId == downloadHandle.EngineJobId && e.Snapshot?.State == SoulseekJobState.Succeeded);

            var secondEvent = await secondSubscription;
            var recoveredSnapshot = await WaitForSnapshotAsync(gateway, downloadHandle.EngineJobId, SoulseekJobState.Succeeded);

            Assert.AreEqual(downloadHandle.EngineJobId, firstEvent.EntityId);
            Assert.AreEqual(downloadHandle.EngineJobId, secondEvent.EntityId);
            Assert.IsTrue(secondEvent.Sequence >= firstEvent.Sequence);
            Assert.AreEqual(downloadHandle.EngineJobId, recoveredSnapshot.EngineJobId);
            Assert.AreEqual(SoulseekJobState.Succeeded, recoveredSnapshot.State);
        }
        finally
        {
            cts.Cancel();
            await runTask;
            Directory.Delete(musicRoot, recursive: true);
            Directory.Delete(outputDir, recursive: true);
        }
    }

    private static async Task<JobSnapshot> WaitForSnapshotAsync(ISoulseekEngineGateway gateway, Guid jobId, SoulseekJobState expectedState, int timeoutMs = 5000)
    {
        using var timeout = new CancellationTokenSource(timeoutMs);
        JobSnapshot? lastSnapshot = null;

        while (!timeout.IsCancellationRequested)
        {
            lastSnapshot = await gateway.GetJobAsync(jobId, CancellationToken.None);
            if (lastSnapshot?.State == expectedState)
                return lastSnapshot;

            try
            {
                await Task.Delay(50, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Assert.Fail($"Timed out waiting for job {jobId} to reach state {expectedState}. Last snapshot: {lastSnapshot}.");
        return null!;
    }

    private static async Task<EngineEventEnvelope> WaitForEnvelopeAsync(
        ISoulseekEngineGateway gateway,
        Guid workflowId,
        Func<EngineEventEnvelope, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        using var timeout = cancellationToken == default ? new CancellationTokenSource(5000) : null;
        var token = timeout?.Token ?? cancellationToken;

        try
        {
            await foreach (var envelope in gateway.SubscribeAsync(workflowId, token))
            {
                if (predicate(envelope))
                    return envelope;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }

        Assert.Fail($"Timed out waiting for a matching event for workflow {workflowId}.");
        return null!;
    }

    private static WebApplication BuildApp(string musicRoot, string outputDir)
        => ServerHost.Build([], new ServerOptions
        {
            Engine = new EngineSettings
            {
                MockFilesDir = musicRoot,
                MockFilesReadTags = false,
            },
            DefaultDownload = new DownloadSettings
            {
                Output =
                {
                    ParentDir = outputDir,
                    NameFormat = "{foldername}/{filename}",
                },
            },
            Profiles = ProfileCatalog.Empty,
            SessionToken = "gateway-test-token",
        }, "http://127.0.0.1:0");

    private static EngineSupervisor CreateSupervisor(string musicRoot, string outputDir, bool slowMockFiles = false)
        => new(Options.Create(new ServerOptions
        {
            Engine = new EngineSettings
            {
                MockFilesDir = musicRoot,
                MockFilesReadTags = false,
                MockFilesSlow = slowMockFiles,
            },
            DefaultDownload = new DownloadSettings
            {
                Output =
                {
                    ParentDir = outputDir,
                    NameFormat = "{foldername}/{filename}",
                },
            },
            Profiles = ProfileCatalog.Empty,
        }));

    private sealed class NoOpHubContext<THub> : IHubContext<THub>
        where THub : Hub
    {
        public IHubClients Clients { get; } = new NoOpHubClients();
        public IGroupManager Groups { get; } = new NoOpGroupManager();
    }

    private sealed class NoOpHubClients : IHubClients
    {
        private static readonly IClientProxy Proxy = new NoOpClientProxy();

        public IClientProxy All => Proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Client(string connectionId) => Proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
        public IClientProxy Group(string groupName) => Proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public IClientProxy User(string userId) => Proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
