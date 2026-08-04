namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class ExternalAccountEntity
{
    public Guid Id { get; set; }
    public int Provider { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SecretReference { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTimeOffset? LastAuthorizedAtUtc { get; set; }

    public List<ExternalPlaylistEntity> Playlists { get; set; } = [];
}
