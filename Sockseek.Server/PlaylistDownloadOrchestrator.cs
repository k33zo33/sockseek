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
                var snapshot = DeserializeSnapshot(item);
                var handle = await gateway.StartTrackSearchAsync(
                    new TrackSearchRequest(snapshot.Artist, snapshot.Title, snapshot.Album, null),
                    cancellationToken);
                var now = DateTimeOffset.UtcNow;

                item.Status = (int)PlaylistItemStatus.Searching;
                dbContext.DownloadWorkflows.Add(new DownloadWorkflowEntity
                {
                    Id = Guid.NewGuid(),
                    WorkflowId = handle.WorkflowId,
                    EngineJobId = handle.EngineJobId,
                    PlaylistItemId = item.Id,
                    Status = (int)DownloadWorkflowPersistenceStatus.Searching,
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
                submissions.Add(new PlaylistDownloadSubmissionRecord(item.Id, handle.WorkflowId, handle.EngineJobId));
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

    private static ExternalPlaylistItemSnapshot DeserializeSnapshot(PlaylistItemEntity item)
        => JsonSerializer.Deserialize<ExternalPlaylistItemSnapshot>(item.SnapshotJson)
            ?? throw new InvalidOperationException($"Playlist item '{item.Id}' snapshot could not be deserialized.");
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

public sealed record PlaylistSearchWorkflowRecord(
    string ProviderItemId,
    string Artist,
    string Title,
    string? Album,
    int? DurationMs);
