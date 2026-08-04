using Sockseek.Domain.Common;

namespace Sockseek.Domain.Workflows;

public sealed class DownloadWorkflow
{
    public DownloadWorkflow(
        Guid workflowId,
        Guid engineJobId,
        DownloadStatus status,
        string? outputPath,
        string? candidateJson,
        string? errorCode,
        DateTimeOffset createdAtUtc,
        EntityId? playlistItemId = null)
    {
        Id = EntityId.New();
        WorkflowId = workflowId;
        EngineJobId = engineJobId;
        Status = status;
        OutputPath = Normalize(outputPath);
        CandidateJson = Normalize(candidateJson);
        ErrorCode = Normalize(errorCode);
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        PlaylistItemId = playlistItemId;
    }

    public EntityId Id { get; }
    public Guid WorkflowId { get; }
    public Guid EngineJobId { get; }
    public EntityId? PlaylistItemId { get; }
    public DownloadStatus Status { get; private set; }
    public string? OutputPath { get; private set; }
    public string? CandidateJson { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void MarkRunning(DateTimeOffset updatedAtUtc)
        => TransitionTo(DownloadStatus.Running, updatedAtUtc);

    public void MarkSucceeded(string outputPath, DateTimeOffset updatedAtUtc)
    {
        TransitionTo(DownloadStatus.Succeeded, updatedAtUtc);
        OutputPath = Require(outputPath, nameof(outputPath));
        ErrorCode = null;
    }

    public void MarkFailed(string errorCode, DateTimeOffset updatedAtUtc)
    {
        TransitionTo(DownloadStatus.Failed, updatedAtUtc);
        ErrorCode = Require(errorCode, nameof(errorCode));
    }

    public void MarkCancelled(DateTimeOffset updatedAtUtc)
    {
        TransitionTo(DownloadStatus.Cancelled, updatedAtUtc);
        ErrorCode = null;
    }

    public void UpdateCandidateSnapshot(string? candidateJson, DateTimeOffset updatedAtUtc)
    {
        CandidateJson = Normalize(candidateJson);
        if (updatedAtUtc > UpdatedAtUtc)
            UpdatedAtUtc = updatedAtUtc;
    }

    private void TransitionTo(DownloadStatus status, DateTimeOffset updatedAtUtc)
    {
        Status = status;
        if (updatedAtUtc > UpdatedAtUtc)
            UpdatedAtUtc = updatedAtUtc;
    }

    private static string Require(string value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} is required.", paramName)
            : value.Trim();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
