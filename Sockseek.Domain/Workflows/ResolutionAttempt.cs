using Sockseek.Domain.Common;

namespace Sockseek.Domain.Workflows;

public sealed class ResolutionAttempt
{
    public ResolutionAttempt(
        EntityId playlistItemId,
        ResolutionMethod method,
        double score,
        ResolutionDecision decision,
        DateTimeOffset createdAtUtc,
        EntityId? candidateTrackId = null,
        Guid? engineJobId = null)
    {
        if (score is < 0d or > 1d)
            throw new ArgumentOutOfRangeException(nameof(score), "score must be between 0 and 1.");

        Id = EntityId.New();
        PlaylistItemId = playlistItemId;
        CandidateTrackId = candidateTrackId;
        EngineJobId = engineJobId;
        Method = method;
        Score = score;
        Decision = decision;
        CreatedAtUtc = createdAtUtc;
    }

    public EntityId Id { get; }
    public EntityId PlaylistItemId { get; }
    public EntityId? CandidateTrackId { get; }
    public Guid? EngineJobId { get; }
    public ResolutionMethod Method { get; }
    public double Score { get; }
    public ResolutionDecision Decision { get; }
    public DateTimeOffset CreatedAtUtc { get; }
}
