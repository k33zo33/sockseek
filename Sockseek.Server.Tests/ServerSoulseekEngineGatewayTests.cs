using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
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
}
