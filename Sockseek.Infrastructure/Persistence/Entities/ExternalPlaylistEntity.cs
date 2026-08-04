namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class ExternalPlaylistEntity
{
    public Guid Id { get; set; }
    public Guid? AccountId { get; set; }
    public int Provider { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string Name { get; set; } = string.Empty;
    public long SnapshotVersion { get; set; }
    public DateTimeOffset LastSyncedAtUtc { get; set; }

    public ExternalAccountEntity? Account { get; set; }
    public List<PlaylistEntity> Playlists { get; set; } = [];
}
