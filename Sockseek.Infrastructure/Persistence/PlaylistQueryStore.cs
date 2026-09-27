using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class PlaylistQueryStore(SockseekDbContext dbContext)
{
    public async Task<IReadOnlyList<PlaylistSummaryRecord>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        var playlists = await dbContext.Playlists
            .AsNoTracking()
            .Include(playlist => playlist.ExternalPlaylist)
            .Include(playlist => playlist.Items)
            .ToListAsync(cancellationToken);

        return playlists
            .OrderByDescending(playlist => playlist.UpdatedAtUtc)
            .ThenBy(playlist => playlist.Name)
            .Select(ToSummaryRecord)
            .ToList();
    }

    public async Task<PlaylistDetailRecord?> GetDetailAsync(Guid playlistId, CancellationToken cancellationToken = default)
    {
        var playlist = await dbContext.Playlists
            .AsNoTracking()
            .Include(entity => entity.ExternalPlaylist)
            .Include(entity => entity.Items)
            .SingleOrDefaultAsync(entity => entity.Id == playlistId, cancellationToken);

        if (playlist == null)
            return null;

        var items = playlist.Items
            .OrderBy(item => item.Position)
            .ThenBy(item => item.ProviderItemId)
            .Select(ToItemRecord)
            .ToList();

        return new PlaylistDetailRecord(
            playlist.Id,
            playlist.Name,
            ToImportModeName(playlist.ImportMode),
            ToProviderId(playlist.ExternalPlaylist?.Provider),
            playlist.ExternalPlaylist?.ExternalId,
            playlist.ExternalPlaylist?.Url,
            playlist.CreatedAtUtc,
            playlist.UpdatedAtUtc,
            playlist.ExternalPlaylist?.LastSyncedAtUtc,
            ToSummary(items),
            items);
    }

    private static PlaylistSummaryRecord ToSummaryRecord(PlaylistEntity playlist)
    {
        var items = playlist.Items.Select(ToItemRecord).ToList();
        return new PlaylistSummaryRecord(
            playlist.Id,
            playlist.Name,
            ToImportModeName(playlist.ImportMode),
            ToProviderId(playlist.ExternalPlaylist?.Provider),
            playlist.ExternalPlaylist?.ExternalId,
            playlist.ExternalPlaylist?.Url,
            playlist.CreatedAtUtc,
            playlist.UpdatedAtUtc,
            playlist.ExternalPlaylist?.LastSyncedAtUtc,
            ToSummary(items));
    }

    private static PlaylistItemRecord ToItemRecord(PlaylistItemEntity item)
    {
        var snapshot = TryDeserializeSnapshot(item.SnapshotJson);
        var status = ToPlaylistItemStatus(item.Status);
        var effectiveStatus = item.RemovedAtUtc.HasValue ? PlaylistItemStatus.RemovedFromSourcePlaylist : status;
        var artists = snapshot?.Artists is { Count: > 0 } snapshotArtists
            ? snapshotArtists
            : string.IsNullOrWhiteSpace(snapshot?.Artist)
                ? []
                : [snapshot.Artist];

        return new PlaylistItemRecord(
            item.Id,
            item.Position,
            item.ProviderItemId,
            item.CanonicalTrackId,
            effectiveStatus.ToString(),
            snapshot?.Title ?? string.Empty,
            artists,
            snapshot?.Album,
            snapshot?.DurationMs,
            snapshot?.Isrc,
            snapshot?.MusicBrainzRecordingId,
            snapshot?.ExternalTrackId,
            snapshot?.ExternalUrl,
            snapshot?.ArtworkUrl,
            item.RemovedAtUtc);
    }

    private static PlaylistResolutionSummaryRecord ToSummary(IReadOnlyList<PlaylistItemRecord> items)
    {
        var total = items.Count;
        var available = 0;
        var unresolved = 0;
        var review = 0;
        var searching = 0;
        var candidateFound = 0;
        var downloading = 0;
        var failed = 0;
        var skipped = 0;
        var removed = 0;

        foreach (var item in items)
        {
            if (!Enum.TryParse<PlaylistItemStatus>(item.Status, out var status))
            {
                unresolved++;
                continue;
            }

            switch (status)
            {
                case PlaylistItemStatus.AvailableLocal:
                    available++;
                    break;
                case PlaylistItemStatus.Imported:
                case PlaylistItemStatus.Unresolved:
                    unresolved++;
                    break;
                case PlaylistItemStatus.ReviewRequired:
                    review++;
                    break;
                case PlaylistItemStatus.Searching:
                    searching++;
                    break;
                case PlaylistItemStatus.CandidateFound:
                    candidateFound++;
                    break;
                case PlaylistItemStatus.Downloading:
                    downloading++;
                    break;
                case PlaylistItemStatus.Failed:
                    failed++;
                    break;
                case PlaylistItemStatus.Skipped:
                    skipped++;
                    break;
                case PlaylistItemStatus.RemovedFromSourcePlaylist:
                    removed++;
                    break;
            }
        }

        return new PlaylistResolutionSummaryRecord(
            total,
            available,
            unresolved,
            review,
            searching,
            candidateFound,
            downloading,
            failed,
            skipped,
            removed);
    }

    private static ExternalPlaylistItemSnapshot? TryDeserializeSnapshot(string snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ExternalPlaylistItemSnapshot>(snapshotJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ToImportModeName(int importMode)
        => Enum.IsDefined(typeof(PlaylistImportMode), importMode)
            ? ((PlaylistImportMode)importMode).ToString()
            : "Unknown";

    private static PlaylistItemStatus ToPlaylistItemStatus(int status)
        => Enum.IsDefined(typeof(PlaylistItemStatus), status)
            ? (PlaylistItemStatus)status
            : PlaylistItemStatus.Unresolved;

    private static string? ToProviderId(int? provider)
        => provider.HasValue && Enum.IsDefined(typeof(ExternalProvider), provider.Value)
            ? (ExternalProvider)provider.Value switch
            {
                ExternalProvider.Spotify => "spotify",
                ExternalProvider.YouTube => "youtube",
                ExternalProvider.Bandcamp => "bandcamp",
                ExternalProvider.MusicBrainz => "musicbrainz",
                _ => ((ExternalProvider)provider.Value).ToString().ToLowerInvariant(),
            }
            : null;
}

public sealed record PlaylistSummaryRecord(
    Guid PlaylistId,
    string Name,
    string ImportMode,
    string? ProviderId,
    string? ExternalPlaylistId,
    string? ExternalUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastSyncedAtUtc,
    PlaylistResolutionSummaryRecord Resolution);

public sealed record PlaylistDetailRecord(
    Guid PlaylistId,
    string Name,
    string ImportMode,
    string? ProviderId,
    string? ExternalPlaylistId,
    string? ExternalUrl,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastSyncedAtUtc,
    PlaylistResolutionSummaryRecord Resolution,
    IReadOnlyList<PlaylistItemRecord> Items);

public sealed record PlaylistItemRecord(
    Guid PlaylistItemId,
    int Position,
    string ProviderItemId,
    Guid? CanonicalTrackId,
    string Status,
    string Title,
    IReadOnlyList<string> Artists,
    string? Album,
    int? DurationMs,
    string? Isrc,
    string? MusicBrainzRecordingId,
    string? ExternalTrackId,
    string? ExternalUrl,
    string? ArtworkUrl,
    DateTimeOffset? RemovedAtUtc);

public sealed record PlaylistResolutionSummaryRecord(
    int TotalItems,
    int AvailableLocalItems,
    int UnresolvedItems,
    int ReviewRequiredItems,
    int SearchingItems,
    int CandidateFoundItems,
    int DownloadingItems,
    int FailedItems,
    int SkippedItems,
    int RemovedItems);
