using Sockseek.Domain.Accounts;
using Sockseek.Domain.Common;

namespace Sockseek.Domain.Playlists;

public sealed class ExternalPlaylist
{
    public ExternalPlaylist(
        ExternalProvider provider,
        string externalId,
        string name,
        string? url,
        EntityId? accountId,
        long snapshotVersion,
        DateTimeOffset syncedAtUtc)
    {
        Id = EntityId.New();
        Provider = provider;
        ExternalId = Require(externalId, nameof(externalId));
        Name = Require(name, nameof(name));
        Url = Normalize(url);
        AccountId = accountId;
        SnapshotVersion = snapshotVersion;
        LastSyncedAtUtc = syncedAtUtc;
    }

    public EntityId Id { get; }
    public ExternalProvider Provider { get; }
    public string ExternalId { get; }
    public string Name { get; private set; }
    public string? Url { get; private set; }
    public EntityId? AccountId { get; }
    public long SnapshotVersion { get; private set; }
    public DateTimeOffset LastSyncedAtUtc { get; private set; }

    public void ApplySnapshot(string name, string? url, long snapshotVersion, DateTimeOffset syncedAtUtc)
    {
        Name = Require(name, nameof(name));
        Url = Normalize(url);

        if (snapshotVersion > SnapshotVersion)
            SnapshotVersion = snapshotVersion;

        if (syncedAtUtc > LastSyncedAtUtc)
            LastSyncedAtUtc = syncedAtUtc;
    }

    private static string Require(string value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} is required.", paramName)
            : value.Trim();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
