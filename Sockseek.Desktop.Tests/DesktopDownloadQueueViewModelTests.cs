using Microsoft.VisualStudio.TestTools.UnitTesting;
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
}