using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Integrations.Abstractions;

namespace Sockseek.Infrastructure.Persistence;

public static class ExternalPlaylistSnapshotRecordFactory
{
    public static ExternalPlaylistSnapshotRecord FromProviderSnapshot(
        ExternalPlaylistSnapshot snapshot,
        PlaylistImportMode importMode,
        ExternalAccountSnapshot? account = null,
        string? playlistName = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (account != null && !StringComparer.Ordinal.Equals(account.ProviderId, snapshot.ProviderId))
            throw new ArgumentException("Account belongs to a different provider than the playlist snapshot.", nameof(account));

        var provider = ToExternalProvider(snapshot.ProviderId);
        return new ExternalPlaylistSnapshotRecord(
            provider,
            snapshot.ExternalPlaylistId,
            snapshot.Name,
            snapshot.Url,
            snapshot.SnapshotVersion,
            snapshot.SyncedAtUtc,
            importMode,
            string.IsNullOrWhiteSpace(playlistName) ? snapshot.Name : playlistName,
            snapshot.Items
                .OrderBy(item => item.Position)
                .Select(ToPlaylistItemSnapshot)
                .ToArray(),
            account == null
                ? null
                : new ExternalAccountRecord(
                    provider,
                    account.ExternalUserId,
                    account.DisplayName,
                    account.SecretReference,
                    account.AuthorizedAtUtc));
    }

    private static ExternalPlaylistItemSnapshot ToPlaylistItemSnapshot(ExternalTrackSnapshot item)
    {
        var artists = item.Artists
            .Where(artist => !string.IsNullOrWhiteSpace(artist))
            .Select(artist => artist.Trim())
            .ToArray();

        return new ExternalPlaylistItemSnapshot(
            item.ProviderItemId,
            item.Position,
            item.Title,
            artists.FirstOrDefault() ?? "Unknown Artist",
            item.Album,
            item.DurationMs,
            item.ExternalTrackId,
            item.Isrc,
            item.ExternalUrl,
            item.ArtworkUrl,
            item.MusicBrainzRecordingId,
            artists,
            item.RawMetadataJson);
    }

    private static ExternalProvider ToExternalProvider(string providerId)
        => providerId switch
        {
            ProviderIds.Spotify => ExternalProvider.Spotify,
            ProviderIds.YouTube => ExternalProvider.YouTube,
            ProviderIds.Bandcamp => ExternalProvider.Bandcamp,
            ProviderIds.MusicBrainz => ExternalProvider.MusicBrainz,
            _ => throw new ArgumentException($"Unsupported external provider '{providerId}'.", nameof(providerId)),
        };
}
