using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http.Json;
using Sockseek.Api;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopDownloadQueueViewModelTests
{
    [TestMethod]
    public void ApplyWorkflowUpdate_CoalescesProgressByJobId()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopDownloadQueueViewModel(new SockseekApiClient(httpClient));
        var jobId = Guid.NewGuid();

        viewModel.ApplyWorkflowUpdate(new WorkflowUpdateBatchDto(
            1,
            DateTimeOffset.UtcNow,
            Guid.Empty,
            null,
            [],
            [],
            [new DownloadProgressEventDto(jobId, Guid.NewGuid(), 10, 100)],
            []));
        viewModel.ApplyWorkflowUpdate(new WorkflowUpdateBatchDto(
            2,
            DateTimeOffset.UtcNow,
            Guid.Empty,
            null,
            [],
            [],
            [new DownloadProgressEventDto(jobId, Guid.NewGuid(), 75, 100)],
            []));

        Assert.AreEqual(1, viewModel.Progress.Count);
        Assert.AreEqual(75, viewModel.Progress[0].BytesTransferred);
    }

    [TestMethod]
    public void ApplyWorkflowUpdate_AddsDownloadNotifications()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopDownloadQueueViewModel(new SockseekApiClient(httpClient));
        var workflowId = Guid.NewGuid();

        viewModel.ApplyWorkflowUpdate(new WorkflowUpdateBatchDto(
            1,
            DateTimeOffset.UtcNow,
            workflowId,
            null,
            [new JobSummaryDto
            {
                JobId = Guid.NewGuid(),
                DisplayId = 7,
                WorkflowId = workflowId,
                Kind = ServerJobKind.Song,
                LifecycleState = ServerJobLifecycleState.Terminal,
                TerminalOutcome = ServerJobTerminalOutcome.Succeeded,
                ItemName = "Song"
            }],
            [],
            [],
            [new ServerEventEnvelopeDto(
                2,
                "download.started",
                DateTimeOffset.UtcNow,
                "activity",
                false,
                workflowId,
                new object())]));

        Assert.AreEqual(2, viewModel.Notifications.Count);
        Assert.AreEqual("Download completed", viewModel.Notifications[0].Title);
        StringAssert.Contains(viewModel.Notifications[0].Message, "#7 Song");
        Assert.AreEqual("Download started", viewModel.Notifications[1].Title);
    }

    [TestMethod]
    public async Task RetryAsync_DelegatesToDaemonRetryEndpoint()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopDownloadQueueViewModel(new SockseekApiClient(httpClient));
        var jobId = Guid.NewGuid();

        Assert.IsTrue(await viewModel.RetryAsync(jobId));

        Assert.AreEqual("api/jobs/" + jobId + "/retry", handler.RequestUri);
        Assert.IsNull(viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenFileAsync_LoadsJobDetailAndDelegatesDownloadPath()
    {
        var jobId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => DetailWithSongDownloadPath(jobId, "C:\\Music\\song.flac"));
        var opener = new FakeFileOpener(openFileResult: true, openFolderResult: true);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopDownloadQueueViewModel(new SockseekApiClient(httpClient), opener);

        Assert.IsTrue(await viewModel.OpenFileAsync(jobId));

        Assert.AreEqual("api/jobs/" + jobId, handler.RequestUri);
        Assert.AreEqual("C:\\Music\\song.flac", opener.OpenedFilePath);
        Assert.IsNull(viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenFolderAsync_ReportsUnavailableWhenNoDownloadPathExists()
    {
        var jobId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new JobDetailDto(new JobSummaryDto { JobId = jobId }, null, []));
        var opener = new FakeFileOpener(openFileResult: true, openFolderResult: true);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopDownloadQueueViewModel(new SockseekApiClient(httpClient), opener);

        Assert.IsFalse(await viewModel.OpenFolderAsync(jobId));

        Assert.IsNull(opener.OpenedFolderPath);
        StringAssert.Contains(viewModel.ErrorMessage, "folder is not available");
    }

    [TestMethod]
    public async Task LoadWorkflowTreeAsync_LoadsWorkflowTreeDetail()
    {
        var workflowId = Guid.NewGuid();
        var root = new JobSummaryDto
        {
            JobId = Guid.NewGuid(),
            DisplayId = 1,
            WorkflowId = workflowId,
            Kind = ServerJobKind.Album,
            ItemName = "Album"
        };
        var child = new JobSummaryDto
        {
            JobId = Guid.NewGuid(),
            DisplayId = 2,
            WorkflowId = workflowId,
            Kind = ServerJobKind.Song,
            ItemName = "Song"
        };
        var handler = new RecordingHandler(_ => new WorkflowTreeDto(
            new WorkflowSummaryDto(workflowId, "Album workflow", ServerWorkflowState.Active, [root.JobId], 1, 0, 0),
            [new WorkflowJobNodeDto(root, [new WorkflowJobNodeDto(child, [])])]));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopDownloadQueueViewModel(new SockseekApiClient(httpClient));

        Assert.IsTrue(await viewModel.LoadWorkflowTreeAsync(root));

        Assert.AreEqual("api/workflows/" + workflowId + "/tree", handler.RequestUri);
        Assert.AreEqual("Album workflow (Active)", viewModel.SelectedWorkflowTitle);
        Assert.IsTrue(viewModel.IsWorkflowTreeVisible);
        Assert.AreEqual(2, viewModel.SelectedWorkflowNodes.Count);
        StringAssert.Contains(viewModel.SelectedWorkflowNodes[1].DisplayLabel, "  #2 Song");
    }

    private static JobDetailDto DetailWithSongDownloadPath(Guid jobId, string downloadPath)
        => new(
            new JobSummaryDto { JobId = jobId, Kind = ServerJobKind.Song },
            new SongJobPayloadDto(new SongQueryDto("Artist", "Title"), 1, downloadPath),
            []);

    private sealed class RecordingHandler(Func<HttpRequestMessage, object>? responseFactory = null) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.PathAndQuery.TrimStart('/');
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = JsonContent.Create(responseFactory?.Invoke(request) ?? new { }),
            });
        }
    }

    private sealed class FakeFileOpener(bool openFileResult, bool openFolderResult) : IDesktopFileOpener
    {
        public string? OpenedFilePath { get; private set; }
        public string? OpenedFolderPath { get; private set; }

        public Task<bool> OpenFileAsync(string path, CancellationToken cancellationToken = default)
        {
            OpenedFilePath = path;
            return Task.FromResult(openFileResult);
        }

        public Task<bool> OpenFolderAsync(string path, CancellationToken cancellationToken = default)
        {
            OpenedFolderPath = path;
            return Task.FromResult(openFolderResult);
        }
    }
}
