using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class ExternalPlaylistSnapshotStore(SockseekDbContext dbContext)
{
    public async Task<Guid> UpsertAsync(ExternalPlaylistSnapshotRecord snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ExternalAccountEntity? account = null;
        if (snapshot.Account is { } accountRecord)
        {
            account = await dbContext.ExternalAccounts
                .SingleOrDefaultAsync(
                    entity => entity.Provider == (int)accountRecord.Provider
                        && entity.ExternalUserId == accountRecord.ExternalUserId,
                    cancellationToken);

            if (account == null)
            {
                account = new ExternalAccountEntity
                {
                    Id = Guid.NewGuid(),
                    Provider = (int)accountRecord.Provider,
                    ExternalUserId = accountRecord.ExternalUserId,
                    DisplayName = accountRecord.DisplayName,
                    SecretReference = accountRecord.SecretReference,
                    Status = 1,
                    LastAuthorizedAtUtc = accountRecord.LastAuthorizedAtUtc,
                };
                dbContext.ExternalAccounts.Add(account);
            }
            else
            {
                account.DisplayName = accountRecord.DisplayName;
                account.SecretReference = accountRecord.SecretReference;
                account.LastAuthorizedAtUtc = accountRecord.LastAuthorizedAtUtc;
                account.Status = 1;
            }
        }

        Guid? accountId = account?.Id;
        var externalPlaylist = await dbContext.ExternalPlaylists
            .Include(entity => entity.Playlists)
            .ThenInclude(entity => entity.Items)
            .SingleOrDefaultAsync(
                entity => entity.Provider == (int)snapshot.Provider
                    && entity.ExternalId == snapshot.ExternalPlaylistId
                    && entity.AccountId == accountId,
                cancellationToken);

        if (externalPlaylist == null)
        {
            externalPlaylist = new ExternalPlaylistEntity
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Provider = (int)snapshot.Provider,
                ExternalId = snapshot.ExternalPlaylistId,
                Url = snapshot.Url,
                Name = snapshot.Name,
                SnapshotVersion = snapshot.SnapshotVersion,
                LastSyncedAtUtc = snapshot.LastSyncedAtUtc,
            };
            dbContext.ExternalPlaylists.Add(externalPlaylist);
        }
        else
        {
            externalPlaylist.Url = snapshot.Url;
            externalPlaylist.Name = snapshot.Name;
            externalPlaylist.SnapshotVersion = Math.Max(externalPlaylist.SnapshotVersion, snapshot.SnapshotVersion);
            if (snapshot.LastSyncedAtUtc > externalPlaylist.LastSyncedAtUtc)
                externalPlaylist.LastSyncedAtUtc = snapshot.LastSyncedAtUtc;
        }

        var playlist = externalPlaylist.Playlists.SingleOrDefault();
        if (playlist == null)
        {
            playlist = new PlaylistEntity
            {
                Id = Guid.NewGuid(),
                Name = snapshot.PlaylistName,
                ImportMode = (int)snapshot.ImportMode,
                ExternalPlaylistId = externalPlaylist.Id,
                CreatedAtUtc = snapshot.LastSyncedAtUtc,
                UpdatedAtUtc = snapshot.LastSyncedAtUtc,
            };
            externalPlaylist.Playlists.Add(playlist);
        }
        else
        {
            playlist.Name = snapshot.PlaylistName;
            playlist.ImportMode = (int)snapshot.ImportMode;
            playlist.UpdatedAtUtc = snapshot.LastSyncedAtUtc;
        }

        var existingItems = playlist.Items.ToDictionary(item => item.ProviderItemId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in snapshot.Items.OrderBy(item => item.Position))
        {
            seen.Add(item.ProviderItemId);
            string serializedSnapshot = JsonSerializer.Serialize(item);

            if (existingItems.TryGetValue(item.ProviderItemId, out var existing))
            {
                existing.Position = item.Position;
                existing.Status = existing.RemovedAtUtc.HasValue ? 0 : existing.Status;
                existing.SnapshotJson = serializedSnapshot;
                existing.RemovedAtUtc = null;
                continue;
            }

            playlist.Items.Add(new PlaylistItemEntity
            {
                Id = Guid.NewGuid(),
                ProviderItemId = item.ProviderItemId,
                Position = item.Position,
                Status = 0,
                SnapshotJson = serializedSnapshot,
            });
        }

        if (snapshot.ImportMode == Domain.Playlists.PlaylistImportMode.Mirror)
        {
            foreach (var item in playlist.Items.Where(item => !seen.Contains(item.ProviderItemId)))
            {
                item.Status = 9;
                item.RemovedAtUtc ??= snapshot.LastSyncedAtUtc;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return playlist.Id;
    }
}
