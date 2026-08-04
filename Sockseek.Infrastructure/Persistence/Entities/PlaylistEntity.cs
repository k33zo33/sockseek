namespace Sockseek.Infrastructure.Persistence.Entities;

using Sockseek.Infrastructure.Persistence.Abstractions;

public sealed class PlaylistEntity : IHasConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ImportMode { get; set; }
    public Guid? ExternalPlaylistId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ExternalPlaylistEntity? ExternalPlaylist { get; set; }
    public List<PlaylistItemEntity> Items { get; set; } = [];
}
