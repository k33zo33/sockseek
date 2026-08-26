using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Common;
using Sockseek.Domain.Workflows;

namespace Sockseek.Domain.Tests.Workflows;

[TestClass]
public class DownloadWorkflowTests
{
    [TestMethod]
    public void Lifecycle_TracksStatusOutputAndTimestamps()
    {
        var createdAt = new DateTimeOffset(2026, 8, 4, 19, 0, 0, TimeSpan.Zero);
        var runningAt = createdAt.AddMinutes(1);
        var succeededAt = createdAt.AddMinutes(2);
        var workflow = new DownloadWorkflow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DownloadStatus.Pending,
            null,
            null,
            null,
            createdAt,
            EntityId.New());

        workflow.MarkRunning(runningAt);
        workflow.UpdateCandidateSnapshot("{\"candidate\":1}", runningAt);
        workflow.MarkSucceeded(" /music/Artist/Track.mp3 ", succeededAt);

        Assert.AreEqual(DownloadStatus.Succeeded, workflow.Status);
        Assert.AreEqual("/music/Artist/Track.mp3", workflow.OutputPath);
        Assert.AreEqual("{\"candidate\":1}", workflow.CandidateJson);
        Assert.IsNull(workflow.ErrorCode);
        Assert.AreEqual(succeededAt, workflow.UpdatedAtUtc);
    }

    [TestMethod]
    public void MarkFailed_StoresErrorCode_AndMarkCancelled_ClearsIt()
    {
        var createdAt = new DateTimeOffset(2026, 8, 4, 19, 0, 0, TimeSpan.Zero);
        var failedAt = createdAt.AddMinutes(1);
        var cancelledAt = createdAt.AddMinutes(2);
        var workflow = new DownloadWorkflow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DownloadStatus.Pending,
            null,
            null,
            null,
            createdAt);

        workflow.MarkFailed(" NETWORK_TIMEOUT ", failedAt);
        Assert.AreEqual(DownloadStatus.Failed, workflow.Status);
        Assert.AreEqual("NETWORK_TIMEOUT", workflow.ErrorCode);

        workflow.MarkCancelled(cancelledAt);
        Assert.AreEqual(DownloadStatus.Cancelled, workflow.Status);
        Assert.IsNull(workflow.ErrorCode);
        Assert.AreEqual(cancelledAt, workflow.UpdatedAtUtc);
    }
}
