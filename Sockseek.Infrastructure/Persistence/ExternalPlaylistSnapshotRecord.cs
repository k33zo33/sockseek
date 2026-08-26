using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;

namespace Sockseek.Infrastructure.Persistence;

public sealed record ExternalPlaylistSnapshotRecord(
    ExternalProvider Provider,
    string ExternalPlaylistId,
    string Name,
    string? Url,
    long SnapshotVersion,
    DateTimeOffset LastSyncedAtUtc,
    PlaylistImportMode ImportMode,
    string PlaylistName,
    IReadOnlyList<ExternalPlaylistItemSnapshot> Items,
    ExternalAccountRecord? Account = null);

public sealed record ExternalAccountRecord(
    ExternalProvider Provider,
    string ExternalUserId,
    string DisplayName,
    string SecretReference,
    DateTimeOffset? LastAuthorizedAtUtc);
