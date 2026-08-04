using Sockseek.Domain.Common;

namespace Sockseek.Domain.Playlists;

public sealed class PlaylistItem
{
    internal PlaylistItem(ExternalPlaylistItemSnapshot snapshot)
    {
        Id = EntityId.New();
        ProviderItemId = Require(snapshot.ProviderItemId, nameof(snapshot.ProviderItemId));
        ApplySnapshot(snapshot);
        Status = PlaylistItemStatus.Imported;
    }

    public EntityId Id { get; }
    public string ProviderItemId { get; }
    public int Position { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Artist { get; private set; } = string.Empty;
    public string? Album { get; private set; }
    public int? DurationMs { get; private set; }
    public PlaylistItemStatus Status { get; private set; }
    public DateTimeOffset? RemovedAtUtc { get; private set; }

    internal void ApplySnapshot(ExternalPlaylistItemSnapshot snapshot)
    {
        Position = snapshot.Position;
        Title = Require(snapshot.Title, nameof(snapshot.Title));
        Artist = Require(snapshot.Artist, nameof(snapshot.Artist));
        Album = string.IsNullOrWhiteSpace(snapshot.Album) ? null : snapshot.Album.Trim();
        DurationMs = snapshot.DurationMs;

        if (Status == PlaylistItemStatus.RemovedFromSourcePlaylist)
        {
            Status = PlaylistItemStatus.Imported;
            RemovedAtUtc = null;
        }
    }

    internal void MarkRemoved(DateTimeOffset removedAtUtc)
    {
        Status = PlaylistItemStatus.RemovedFromSourcePlaylist;
        RemovedAtUtc ??= removedAtUtc;
    }

    private static string Require(string value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} is required.", paramName)
            : value.Trim();
}
