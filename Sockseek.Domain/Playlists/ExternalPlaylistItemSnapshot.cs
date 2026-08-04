namespace Sockseek.Domain.Playlists;

public sealed record ExternalPlaylistItemSnapshot(
    string ProviderItemId,
    int Position,
    string Title,
    string Artist,
    string? Album,
    int? DurationMs);
