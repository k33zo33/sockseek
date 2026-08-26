using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Common;
using Sockseek.Domain.Workflows;

namespace Sockseek.Domain.Tests.Workflows;

[TestClass]
public class ResolutionAttemptTests
{
    [TestMethod]
    public void Constructor_PreservesWorkflowReferences()
    {
        var playlistItemId = EntityId.New();
        var candidateTrackId = EntityId.New();
        var engineJobId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 8, 4, 19, 0, 0, TimeSpan.Zero);

        var attempt = new ResolutionAttempt(
            playlistItemId,
            ResolutionMethod.SoulseekSearch,
            0.81d,
            ResolutionDecision.DownloadRequested,
            createdAt,
            candidateTrackId,
            engineJobId);

        Assert.AreEqual(playlistItemId, attempt.PlaylistItemId);
        Assert.AreEqual(candidateTrackId, attempt.CandidateTrackId);
        Assert.AreEqual(engineJobId, attempt.EngineJobId);
        Assert.AreEqual(ResolutionMethod.SoulseekSearch, attempt.Method);
        Assert.AreEqual(ResolutionDecision.DownloadRequested, attempt.Decision);
        Assert.AreEqual(0.81d, attempt.Score, 0.0001d);
    }

    [TestMethod]
    public void Constructor_RejectsOutOfRangeScore()
    {
        var playlistItemId = EntityId.New();

        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            new ResolutionAttempt(
                playlistItemId,
                ResolutionMethod.ManualReview,
                1.5d,
                ResolutionDecision.UserRejected,
                DateTimeOffset.UtcNow));
    }
}
