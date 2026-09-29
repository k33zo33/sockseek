using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sockseek.Application.Soulseek;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Server;

public sealed class PlaylistWorkflowSyncService(
    SockseekDbContext dbContext,
    ISoulseekEngineGateway gateway,
    CanonicalTrackStore trackStore)
{
    public Task<int> SyncAllAsync(CancellationToken cancellationToken = default)
        => SyncAsync(playlistId: null, cancellationToken);

    public Task<int> SyncPlaylistAsync(Guid playlistId, CancellationToken cancellationToken = default)
        => SyncAsync(playlistId, cancellationToken);

    private async Task<int> SyncAsync(Guid? playlistId, CancellationToken cancellationToken)
    {
        var workflows = await dbContext.DownloadWorkflows
            .Include(workflow => workflow.PlaylistItem)
            .ThenInclude(item => item!.Playlist)
            .ThenInclude(playlist => playlist.ExternalPlaylist)
            .Where(workflow => workflow.PlaylistItemId.HasValue
                && workflow.PlaylistItem != null
                && (playlistId == null || workflow.PlaylistItem.PlaylistId == playlistId)
                && (workflow.Status == (int)DownloadWorkflowPersistenceStatus.Searching
                    || workflow.Status == (int)DownloadWorkflowPersistenceStatus.Downloading))
            .ToListAsync(cancellationToken);

        var changed = 0;
        foreach (var workflow in workflows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await gateway.GetDownloadResultAsync(workflow.EngineJobId, cancellationToken);
            if (result == null)
                continue;

            changed += await ApplyResultAsync(workflow, result, cancellationToken);
        }

        return changed;
    }

    private async Task<int> ApplyResultAsync(
        DownloadWorkflowEntity workflow,
        DownloadJobResultSnapshot result,
        CancellationToken cancellationToken)
    {
        var item = workflow.PlaylistItem;
        if (item == null)
            return 0;

        return result.State switch
        {
            SoulseekJobState.Succeeded => await MarkSucceededAsync(workflow, item, result, cancellationToken),
            SoulseekJobState.Cancelled => await MarkTerminalAsync(
                workflow,
                item,
                DownloadWorkflowPersistenceStatus.Cancelled,
                "cancelled",
                cancellationToken),
            SoulseekJobState.Failed => await MarkTerminalAsync(
                workflow,
                item,
                DownloadWorkflowPersistenceStatus.Failed,
                "download_failed",
                cancellationToken),
            _ => await MarkRunningAsync(workflow, item, cancellationToken),
        };
    }

    private async Task<int> MarkSucceededAsync(
        DownloadWorkflowEntity workflow,
        PlaylistItemEntity item,
        DownloadJobResultSnapshot result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(result.OutputPath) || !File.Exists(result.OutputPath))
        {
            return await MarkTerminalAsync(
                workflow,
                item,
                DownloadWorkflowPersistenceStatus.Failed,
                "download_output_missing",
                cancellationToken);
        }

        var fileInfo = new FileInfo(result.OutputPath);
        var snapshot = DeserializeSnapshot(item);
        var trackId = await trackStore.UpsertAsync(
            new CanonicalTrackRecord(
                snapshot.Artist,
                snapshot.Title,
                snapshot.Album,
                snapshot.DurationMs,
                snapshot.Isrc,
                snapshot.MusicBrainzRecordingId,
                CreateTrackSources(item, snapshot),
                [
                    new LocalMediaFileRecord(
                        NormalizePath(fileInfo.FullName),
                        fileInfo.Length,
                        new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero),
                        snapshot.DurationMs,
                        InferCodec(fileInfo.Extension),
                        Bitrate: null,
                        SampleRate: null,
                        BitDepth: null,
                        LocalMediaAvailability.Available),
                ]),
            cancellationToken);

        item.CanonicalTrackId = trackId;
        if (!item.RemovedAtUtc.HasValue)
            item.Status = (int)PlaylistItemStatus.AvailableLocal;
        workflow.Status = (int)DownloadWorkflowPersistenceStatus.Succeeded;
        workflow.OutputPath = NormalizePath(fileInfo.FullName);
        workflow.ErrorCode = null;
        workflow.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return 1;
    }

    private async Task<int> MarkTerminalAsync(
        DownloadWorkflowEntity workflow,
        PlaylistItemEntity item,
        DownloadWorkflowPersistenceStatus status,
        string errorCode,
        CancellationToken cancellationToken)
    {
        workflow.Status = (int)status;
        workflow.ErrorCode = errorCode;
        workflow.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (!item.RemovedAtUtc.HasValue && !item.CanonicalTrackId.HasValue)
            item.Status = (int)PlaylistItemStatus.Failed;

        await dbContext.SaveChangesAsync(cancellationToken);
        return 1;
    }

    private async Task<int> MarkRunningAsync(
        DownloadWorkflowEntity workflow,
        PlaylistItemEntity item,
        CancellationToken cancellationToken)
    {
        var changed = false;
        if (workflow.Status != (int)DownloadWorkflowPersistenceStatus.Downloading)
        {
            workflow.Status = (int)DownloadWorkflowPersistenceStatus.Downloading;
            changed = true;
        }

        if (!item.RemovedAtUtc.HasValue
            && !item.CanonicalTrackId.HasValue
            && item.Status != (int)PlaylistItemStatus.Downloading)
        {
            item.Status = (int)PlaylistItemStatus.Downloading;
            changed = true;
        }

        if (!changed)
            return 0;

        workflow.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return 1;
    }

    private static IReadOnlyList<TrackSourceRecord> CreateTrackSources(
        PlaylistItemEntity item,
        ExternalPlaylistItemSnapshot snapshot)
    {
        var externalPlaylist = item.Playlist.ExternalPlaylist;
        if (externalPlaylist == null || !Enum.IsDefined(typeof(ExternalProvider), externalPlaylist.Provider))
            return [];

        var externalId = string.IsNullOrWhiteSpace(snapshot.ExternalTrackId)
            ? item.ProviderItemId
            : snapshot.ExternalTrackId.Trim();

        if (string.IsNullOrWhiteSpace(externalId))
            return [];

        return
        [
            new TrackSourceRecord(
                (ExternalProvider)externalPlaylist.Provider,
                externalId,
                snapshot.ExternalUrl,
                snapshot.RawMetadataJson),
        ];
    }

    private static ExternalPlaylistItemSnapshot DeserializeSnapshot(PlaylistItemEntity item)
        => JsonSerializer.Deserialize<ExternalPlaylistItemSnapshot>(item.SnapshotJson)
            ?? throw new InvalidOperationException($"Playlist item '{item.Id}' snapshot could not be deserialized.");

    private static string NormalizePath(string path)
        => path.Trim().Replace('\\', '/');

    private static string? InferCodec(string extension)
    {
        var codec = extension.Trim().TrimStart('.').ToLowerInvariant();
        return codec.Length == 0 ? null : codec;
    }
}
