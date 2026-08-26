using Sockseek.Domain.Common;

namespace Sockseek.Domain.Playlists;

public sealed class Playlist
{
    private readonly List<PlaylistItem> items = [];

    public Playlist(string name, PlaylistImportMode importMode, DateTimeOffset createdAtUtc)
    {
        Id = EntityId.New();
        Name = Require(name, nameof(name));
        ImportMode = importMode;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public EntityId Id { get; }
    public string Name { get; }
    public PlaylistImportMode ImportMode { get; }
    public EntityId? ExternalPlaylistId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public IReadOnlyList<PlaylistItem> Items => items;

    public void AttachExternalPlaylist(EntityId externalPlaylistId)
        => ExternalPlaylistId = externalPlaylistId;

    public void ApplyImportSnapshot(IEnumerable<ExternalPlaylistItemSnapshot> snapshots, DateTimeOffset importedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var snapshotList = snapshots.ToList();
        var duplicateProviderItemId = snapshotList
            .GroupBy(snapshot => snapshot.ProviderItemId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;

        if (duplicateProviderItemId != null)
            throw new ArgumentException($"Duplicate provider item id '{duplicateProviderItemId}' in snapshot.", nameof(snapshots));

        var byProviderItemId = items.ToDictionary(item => item.ProviderItemId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var snapshot in snapshotList.OrderBy(snapshot => snapshot.Position))
        {
            seen.Add(snapshot.ProviderItemId);

            if (byProviderItemId.TryGetValue(snapshot.ProviderItemId, out var existing))
            {
                existing.ApplySnapshot(snapshot);
                continue;
            }

            items.Add(new PlaylistItem(snapshot));
        }

        if (ImportMode == PlaylistImportMode.Mirror)
        {
            foreach (var item in items.Where(item => !seen.Contains(item.ProviderItemId)))
                item.MarkRemoved(importedAtUtc);
        }

        UpdatedAtUtc = importedAtUtc;
    }

    private static string Require(string value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} is required.", paramName)
            : value.Trim();
}
