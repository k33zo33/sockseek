using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sockseek.Application.Soulseek;
using Sockseek.Domain.Playlists;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Server;

public sealed class PlaylistDownloadOrchestrator(
    SockseekDbContext dbContext,
    ISoulseekEngineGateway gateway)
{
    public async Task<PlaylistDownloadMissingResult> DownloadMissingAsync(
        Guid playlistId,
        CancellationToken cancellationToken = default)
    {
        var playlistExists = await dbContext.Playlists
            .AsNoTracking()
            .AnyAsync(playlist => playlist.Id == playlistId, cancellationToken);
        if (!playlistExists)
            return PlaylistDownloadMissingResult.NotFound;

        var items = await dbContext.PlaylistItems
            .Where(item => item.PlaylistId == playlistId
                && item.RemovedAtUtc == null
                && item.CanonicalTrackId == null
                && (item.Status == (int)PlaylistItemStatus.Imported
                    || item.Status == (int)PlaylistItemStatus.Unresolved))
            .OrderBy(item => item.Position)
            .ThenBy(item => item.ProviderItemId)
            .ToListAsync(cancellationToken);

        var submissions = new List<PlaylistDownloadSubmissionRecord>();
        var failedItems = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                submissions.Add(await SubmitDownloadAsync(item, cancellationToken));
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                item.Status = (int)PlaylistItemStatus.Failed;
                await dbContext.SaveChangesAsync(cancellationToken);
                failedItems++;
            }
        }

        return new PlaylistDownloadMissingResult(
            PlaylistFound: true,
            SubmittedItems: submissions.Count,
            FailedItems: failedItems,
            SkippedItems: 0,
            Submissions: submissions);
    }

    public async Task<PlaylistItemRetryResult> RetryItemAsync(
        Guid playlistId,
        Guid playlistItemId,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.PlaylistItems
            .SingleOrDefaultAsync(entity => entity.Id == playlistItemId && entity.PlaylistId == playlistId, cancellationToken);
        if (item == null)
            return PlaylistItemRetryResult.NotFound;
        if (item.RemovedAtUtc.HasValue)
            return new PlaylistItemRetryResult(true, PlaylistItemRetryOutcome.Removed, null);
        if (item.CanonicalTrackId.HasValue || item.Status == (int)PlaylistItemStatus.AvailableLocal)
            return new PlaylistItemRetryResult(true, PlaylistItemRetryOutcome.AlreadyAvailable, null);
        var status = Enum.IsDefined(typeof(PlaylistItemStatus), item.Status)
            ? (PlaylistItemStatus)item.Status
            : PlaylistItemStatus.Unresolved;
        if (status is not (PlaylistItemStatus.Failed or PlaylistItemStatus.Skipped))
            return new PlaylistItemRetryResult(true, PlaylistItemRetryOutcome.NotRetryable, null);

        try
        {
            var submission = await SubmitDownloadAsync(item, cancellationToken);
            return new PlaylistItemRetryResult(true, PlaylistItemRetryOutcome.Submitted, submission);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            item.Status = (int)PlaylistItemStatus.Failed;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new PlaylistItemRetryResult(true, PlaylistItemRetryOutcome.FailedToSubmit, null);
        }
    }

    public async Task<PlaylistCancelDownloadsResult> CancelActiveDownloadsAsync(
        Guid playlistId,
        CancellationToken cancellationToken = default)
    {
        var playlistExists = await dbContext.Playlists
            .AsNoTracking()
            .AnyAsync(playlist => playlist.Id == playlistId, cancellationToken);
        if (!playlistExists)
            return PlaylistCancelDownloadsResult.NotFound;

        var workflows = await dbContext.DownloadWorkflows
            .Include(workflow => workflow.PlaylistItem)
            .Where(workflow => workflow.PlaylistItem != null
                && workflow.PlaylistItem.PlaylistId == playlistId
                && workflow.PlaylistItem.RemovedAtUtc == null
                && (workflow.Status == (int)DownloadWorkflowPersistenceStatus.Searching
                    || workflow.Status == (int)DownloadWorkflowPersistenceStatus.Downloading))
            .OrderBy(workflow => workflow.Id)
            .ToListAsync(cancellationToken);

        var cancelledItems = 0;
        var failedItems = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var workflow in workflows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await gateway.CancelJobAsync(workflow.EngineJobId, cancellationToken);
                workflow.Status = (int)DownloadWorkflowPersistenceStatus.Cancelled;
                workflow.ErrorCode = "cancelled_by_user";
                cancelledItems++;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                workflow.Status = (int)DownloadWorkflowPersistenceStatus.Failed;
                workflow.ErrorCode = "cancel_failed";
                failedItems++;
            }

            workflow.UpdatedAtUtc = now;
            if (workflow.PlaylistItem is { } item && IsActivePlaylistStatus(item.Status))
                item.Status = (int)PlaylistItemStatus.Failed;
        }

        if (workflows.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        return new PlaylistCancelDownloadsResult(
            PlaylistFound: true,
            CancelledItems: cancelledItems,
            FailedItems: failedItems);
    }

    private async Task<PlaylistDownloadSubmissionRecord> SubmitDownloadAsync(
        PlaylistItemEntity item,
        CancellationToken cancellationToken)
    {
        var snapshot = DeserializeSnapshot(item);
        var handle = await gateway.StartTrackDownloadAsync(
            new TrackSearchRequest(snapshot.Artist, snapshot.Title, snapshot.Album, null),
            new DownloadOptions(OutputParentDir: null, ProfileName: null),
            cancellationToken);
        var now = DateTimeOffset.UtcNow;

        item.Status = (int)PlaylistItemStatus.Downloading;
        dbContext.DownloadWorkflows.Add(new DownloadWorkflowEntity
        {
            Id = Guid.NewGuid(),
            WorkflowId = handle.WorkflowId,
            EngineJobId = handle.EngineJobId,
            PlaylistItemId = item.Id,
            Status = (int)DownloadWorkflowPersistenceStatus.Downloading,
            CandidateJson = JsonSerializer.Serialize(new PlaylistSearchWorkflowRecord(
                item.ProviderItemId,
                snapshot.Artist,
                snapshot.Title,
                snapshot.Album,
                snapshot.DurationMs)),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new PlaylistDownloadSubmissionRecord(item.Id, handle.WorkflowId, handle.EngineJobId);
    }

    private static ExternalPlaylistItemSnapshot DeserializeSnapshot(PlaylistItemEntity item)
        => JsonSerializer.Deserialize<ExternalPlaylistItemSnapshot>(item.SnapshotJson)
            ?? throw new InvalidOperationException($"Playlist item '{item.Id}' snapshot could not be deserialized.");

    private static bool IsActivePlaylistStatus(int status)
    {
        var playlistStatus = Enum.IsDefined(typeof(PlaylistItemStatus), status)
            ? (PlaylistItemStatus)status
            : PlaylistItemStatus.Unresolved;

        return playlistStatus is PlaylistItemStatus.Searching
            or PlaylistItemStatus.CandidateFound
            or PlaylistItemStatus.Downloading;
    }
}

public enum DownloadWorkflowPersistenceStatus
{
    Searching = 0,
    Downloading = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
}

public sealed record PlaylistDownloadMissingResult(
    bool PlaylistFound,
    int SubmittedItems,
    int FailedItems,
    int SkippedItems,
    IReadOnlyList<PlaylistDownloadSubmissionRecord> Submissions)
{
    public static PlaylistDownloadMissingResult NotFound { get; } = new(
        PlaylistFound: false,
        SubmittedItems: 0,
        FailedItems: 0,
        SkippedItems: 0,
        Submissions: []);
}

public sealed record PlaylistDownloadSubmissionRecord(
    Guid PlaylistItemId,
    Guid WorkflowId,
    Guid EngineJobId);

public sealed record PlaylistCancelDownloadsResult(
    bool PlaylistFound,
    int CancelledItems,
    int FailedItems)
{
    public static PlaylistCancelDownloadsResult NotFound { get; } = new(
        PlaylistFound: false,
        CancelledItems: 0,
        FailedItems: 0);
}

public enum PlaylistItemRetryOutcome
{
    Submitted,
    FailedToSubmit,
    Removed,
    AlreadyAvailable,
    NotRetryable,
}

public sealed record PlaylistItemRetryResult(
    bool ItemFound,
    PlaylistItemRetryOutcome Outcome,
    PlaylistDownloadSubmissionRecord? Submission)
{
    public static PlaylistItemRetryResult NotFound { get; } = new(
        ItemFound: false,
        Outcome: PlaylistItemRetryOutcome.NotRetryable,
        Submission: null);
}

public sealed record PlaylistSearchWorkflowRecord(
    string ProviderItemId,
    string Artist,
    string Title,
    string? Album,
    int? DurationMs);
