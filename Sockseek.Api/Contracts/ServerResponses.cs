using System.Text.Json.Serialization;
using Sockseek.Core;

namespace Sockseek.Api;

/// <summary>
/// Basic daemon identity.
/// </summary>
public sealed record ServerInfoDto(
    string Name,
    string Version,
    DateTimeOffset StartedAtUtc);

/// <summary>
/// Versioned application API system information for the future desktop client.
/// </summary>
public sealed record SystemInfoDto(
    string Name,
    string Version,
    string Commit,
    DateTimeOffset StartedAtUtc,
    SystemCapabilitiesDto Capabilities);

/// <summary>
/// Lightweight readiness/health response for daemon process checks.
/// </summary>
public sealed record SystemHealthDto(
    string Status,
    DateTimeOffset StartedAtUtc,
    int RestartCount,
    string CorrelationId);

/// <summary>
/// Snapshot of application-layer capabilities exposed by the versioned API.
/// </summary>
public sealed record SystemCapabilitiesDto(
    bool LegacyApi,
    bool VersionedApi,
    bool SignalR,
    bool StructuredErrors,
    bool CorrelationIds);

/// <summary>
/// Current daemon and engine activity counters.
/// </summary>
public sealed record ServerStatusDto(
    SoulseekClientStatusDto SoulseekClient,
    int TotalJobCount,
    int ActiveJobCount,
    int TotalWorkflowCount,
    int ActiveWorkflowCount,
    int RestartCount);

/// <summary>
/// Current Soulseek client connection state.
/// </summary>
/// <param name="State">Combined Soulseek.NET client state string.</param>
/// <param name="Flags">Individual Soulseek.NET state flag names.</param>
/// <param name="IsReady">True when the client is both connected and logged in.</param>
public sealed record SoulseekClientStatusDto(
    string State,
    IReadOnlyList<string> Flags,
    bool IsReady);

/// <summary>
/// User-visible summary of a configured profile.
/// </summary>
public sealed record ProfileSummaryDto(
    string Name,
    string? Condition,
    bool IsAutoProfile,
    bool HasEngineSettings,
    bool HasDownloadSettings);

/// <summary>
/// Error response body for rejected API requests.
/// </summary>
public sealed record ApiErrorDto(
    string Error);

/// <summary>
/// Structured error response for versioned application API endpoints.
/// </summary>
public sealed record AppErrorDto(
    string Code,
    string Message,
    string CorrelationId);

/// <summary>
/// Response body returned when cancelling a workflow.
/// </summary>
public sealed record CancelWorkflowResponseDto(
    int Cancelled);

/// <summary>
/// Discoverable mutation affordance. Clients should prefer this over hard-coding job states.
/// </summary>
/// <param name="Kind">Action kind, for example ServerProtocol.ResourceActionKinds.Cancel.</param>
/// <param name="Method">HTTP method to invoke.</param>
/// <param name="Href">Server-relative URL for the action.</param>
public sealed record ResourceActionDto(
    ServerResourceActionKind Kind,
    string Method,
    string Href);

/// <summary>
/// Lightweight job list item. Fetch JobDetailDto for a selected job's typed payload.
/// </summary>
/// <param name="Kind">Stable job kind.</param>
/// <param name="LifecycleState">High-level lifecycle state.</param>
/// <param name="ActivityPhase">Current activity phase for non-terminal jobs.</param>
/// <param name="TerminalOutcome">Terminal result when LifecycleState is Terminal.</param>
/// <param name="SkipReason">Reason when TerminalOutcome is Skipped.</param>
/// <param name="FailureReason">Stable failure reason when TerminalOutcome is failed or cancelled.</param>
/// <param name="CancellationSource">Source of a cancellation outcome, when known.</param>
/// <param name="ParentJobId">Execution parent. Parent cancellation propagates to this job.</param>
/// <param name="ResultJobId">For extract jobs, the semantic result job produced by extraction.</param>
/// <param name="SourceJobId">Provenance link for independently submitted follow-up jobs, such as downloads started from search results.</param>
/// <param name="AvailableActions">Actions currently valid for this job.</param>
/// <param name="PrintOption">Effective print mode for this job, when print-only behavior is active.</param>
public sealed record JobSummaryDto(
    Guid JobId,
    int DisplayId,
    Guid WorkflowId,
    ServerJobKind Kind,
    ServerJobLifecycleState LifecycleState,
    ServerJobActivityPhase ActivityPhase,
    DateTimeOffset? ActivityUntilUtc,
    ServerJobTerminalOutcome TerminalOutcome,
    ServerJobSkipReason SkipReason,
    string? ItemName,
    string? QueryText,
    ServerJobFailureReason? FailureReason,
    string? FailureMessage,
    Guid? ParentJobId,
    Guid? ResultJobId,
    Guid? SourceJobId,
    int? DiscoveryRawResultCount,
    int? DiscoveryLockedFileCount,
    IReadOnlyList<string> AppliedAutoProfiles,
    IReadOnlyList<ResourceActionDto> AvailableActions,
    string? FailureDetail = null,
    ServerJobCancellationSource CancellationSource = ServerJobCancellationSource.None,
    PrintOption PrintOption = PrintOption.None)
{
    public JobSummaryDto()
        : this(
            Guid.Empty,
            0,
            Guid.Empty,
            ServerJobKind.Generic,
            ServerJobLifecycleState.Pending,
            ServerJobActivityPhase.None,
            null,
            ServerJobTerminalOutcome.None,
            ServerJobSkipReason.None,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            null,
            ServerJobCancellationSource.None)
    {
    }

    public JobSummaryDto(
        Guid JobId,
        int DisplayId,
        Guid WorkflowId,
        ServerJobKind Kind,
        ServerJobLifecycleState LifecycleState,
        ServerJobActivityPhase ActivityPhase,
        DateTimeOffset? ActivityUntilUtc,
        ServerJobTerminalOutcome TerminalOutcome,
        string? ItemName,
        string? QueryText,
        ServerJobFailureReason? FailureReason,
        string? FailureMessage,
        Guid? ParentJobId,
        Guid? ResultJobId,
        Guid? SourceJobId,
        int? DiscoveryRawResultCount,
        int? DiscoveryLockedFileCount,
        IReadOnlyList<string> AppliedAutoProfiles,
        IReadOnlyList<ResourceActionDto> AvailableActions,
        string? FailureDetail = null)
        : this(
            JobId,
            DisplayId,
            WorkflowId,
            Kind,
            LifecycleState,
            ActivityPhase,
            ActivityUntilUtc,
            TerminalOutcome,
            ServerJobSkipReason.None,
            ItemName,
            QueryText,
            FailureReason,
            FailureMessage,
            ParentJobId,
            ResultJobId,
            SourceJobId,
            DiscoveryRawResultCount,
            DiscoveryLockedFileCount,
            AppliedAutoProfiles,
            AvailableActions,
            FailureDetail)
    {
    }

}

/// <summary>
/// Selected-job snapshot: summary, typed payload, and direct child summaries for client navigation.
/// </summary>
public sealed record JobDetailDto(
    JobSummaryDto Summary,
    JobPayloadDto? Payload,
    IReadOnlyList<JobSummaryDto> Children);

/// <summary>
/// Workflow list item summarizing related jobs submitted under one workflow id.
/// </summary>
public sealed record WorkflowSummaryDto(
    Guid WorkflowId,
    string Title,
    ServerWorkflowState State,
    IReadOnlyList<Guid> RootJobIds,
    int ActiveJobCount,
    int FailedJobCount,
    int CompletedJobCount);

/// <summary>
/// Workflow snapshot containing execution-root job summaries unless IncludeAll is requested.
/// </summary>
public sealed record WorkflowDetailDto(
    WorkflowSummaryDto Summary,
    IReadOnlyList<JobSummaryDto> Jobs);

/// <summary>
/// Recursive execution tree node built from ParentJobId relationships.
/// </summary>
public sealed record WorkflowJobNodeDto(
    JobSummaryDto Summary,
    IReadOnlyList<WorkflowJobNodeDto> Children);

/// <summary>
/// Workflow snapshot shaped as an execution tree.
/// </summary>
public sealed record WorkflowTreeDto(
    WorkflowSummaryDto Summary,
    IReadOnlyList<WorkflowJobNodeDto> Jobs);

/// <summary>
/// Query parameters for listing jobs.
/// </summary>
/// <param name="IncludeAll">
/// When true, includes every matching job as a flat list. Default lists return only execution roots where ParentJobId is null.
/// </param>
public sealed record JobQuery(
    ServerJobLifecycleState? LifecycleState,
    ServerJobTerminalOutcome? TerminalOutcome,
    ServerJobKind? Kind,
    Guid? WorkflowId,
    bool IncludeAll,
    ServerJobSkipReason? SkipReason = null);

public sealed record LibraryRootDto(
    Guid Id,
    string Path,
    string? DisplayName,
    bool Enabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastScanStartedUtc,
    DateTimeOffset? LastScanCompletedUtc);

public sealed record LocalLibraryTrackDto(
    Guid TrackId,
    string Artist,
    string Title,
    int? DurationMs,
    string? Isrc,
    string? MusicBrainzRecordingId,
    int AvailableFileCount,
    int MissingFileCount,
    Guid? BestAvailableFileId,
    string? BestAvailablePath,
    string? Codec,
    int? Bitrate,
    int? SampleRate,
    int? BitDepth);

public sealed record LocalLibrarySearchResponseDto(
    int TotalCount,
    IReadOnlyList<LocalLibraryTrackDto> Items);

public sealed record LocalLibraryScanResultDto(
    int DiscoveredFiles,
    int ScannedFiles,
    int ImportedFiles,
    int SkippedFiles,
    int FailedFiles,
    int MissingFiles);

public sealed record LocalLibraryConfiguredScanResultDto(
    IReadOnlyList<Guid> RootIds,
    LocalLibraryScanResultDto ScanResult);

public sealed record LocalLibraryDuplicateFileDto(
    Guid LocalMediaFileId,
    string Path,
    long Size,
    int? DurationMs,
    string? Codec,
    int? Bitrate,
    int? SampleRate,
    int? BitDepth,
    LocalMediaAvailabilityDto Availability);

public sealed record LocalLibraryDuplicateGroupDto(
    Guid TrackId,
    string Artist,
    string Title,
    int? DurationMs,
    int FileCount,
    IReadOnlyList<LocalLibraryDuplicateFileDto> Files);

public sealed record LocalMediaFileRelinkResultDto(
    Guid LocalMediaFileId,
    Guid? CanonicalTrackId,
    string Path);
