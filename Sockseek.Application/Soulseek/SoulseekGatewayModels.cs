namespace Sockseek.Application.Soulseek;

public sealed record TrackSearchRequest(
    string Artist,
    string Title,
    string? Album,
    string? FilterText);

public sealed record AlbumSearchRequest(
    string Artist,
    string Album,
    string? FilterText);

public sealed record CandidateReference(
    Guid SourceJobId,
    string Username,
    string Filename);

public sealed record DownloadOptions(
    string? OutputParentDir,
    string? ProfileName);

public sealed record SearchHandle(
    Guid WorkflowId,
    Guid EngineJobId);

public sealed record DownloadHandle(
    Guid WorkflowId,
    Guid EngineJobId);

public enum SoulseekJobKind
{
    TrackSearch,
    AlbumSearch,
    Download,
}

public enum SoulseekJobState
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}

public sealed record JobSnapshot(
    Guid EngineJobId,
    Guid WorkflowId,
    SoulseekJobKind Kind,
    SoulseekJobState State,
    string? Description);

public sealed record EngineEventEnvelope(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    Guid? WorkflowId,
    Guid? EntityId,
    long Sequence,
    JobSnapshot? Snapshot);
