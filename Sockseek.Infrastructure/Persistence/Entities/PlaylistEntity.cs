namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class PlaylistEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ImportMode { get; set; }
    public Guid? ExternalPlaylistId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ExternalPlaylistEntity? ExternalPlaylist { get; set; }
    public List<PlaylistItemEntity> Items { get; set; } = [];
}
