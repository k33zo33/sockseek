using Sockseek.Application.Security;
using Sockseek.Integrations.Abstractions;

namespace Sockseek.Integrations.Fake;

public sealed class FakePlaylistSourceProvider(ISecretStore secretStore) : IPlaylistSourceProvider
{
    private readonly Dictionary<string, FakePlaylistRecord> playlists = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, FakeAccountRecord> accounts = new();
    private readonly object gate = new();

    public string ProviderId => ProviderIds.Fake;

    public PlaylistProviderCapabilities Capabilities =>
        PlaylistProviderCapabilities.ConnectAccount
        | PlaylistProviderCapabilities.ListUserPlaylists
        | PlaylistProviderCapabilities.ReadPlaylistItems
        | PlaylistProviderCapabilities.IncrementalSync;

    public Task<AuthorizationStartResult> StartAuthorizationAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!StringComparer.Ordinal.Equals(request.ProviderId, ProviderId))
            throw new ArgumentException("Authorization request is for a different provider.", nameof(request));

        var builder = new UriBuilder(request.RedirectUri)
        {
            Query = $"code=fake-code&state={Uri.EscapeDataString(request.State)}",
        };
        return Task.FromResult(new AuthorizationStartResult(builder.Uri, request.State));
    }

    public async Task<ExternalAccountSnapshot> CompleteAuthorizationAsync(
        AuthorizationCallback callback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (!StringComparer.Ordinal.Equals(callback.ProviderId, ProviderId))
            throw new ArgumentException("Authorization callback is for a different provider.", nameof(callback));
        if (!string.IsNullOrWhiteSpace(callback.Error))
            throw new InvalidOperationException($"Fake provider authorization failed: {callback.Error}");
        ArgumentException.ThrowIfNullOrWhiteSpace(callback.Code);

        var secretReference = await secretStore.SaveAsync(new SecretStoreSaveRequest(
            ProviderId,
            new Dictionary<string, string>
            {
                ["access_token"] = "fake-access-token",
                ["refresh_token"] = "fake-refresh-token",
            }), cancellationToken);

        var account = new FakeAccountRecord(
            new ExternalAccountId(Guid.NewGuid()),
            "fake-user",
            "Fake User",
            secretReference,
            DateTimeOffset.UtcNow);

        lock (gate)
            accounts[account.AccountId.Value] = account;

        return new ExternalAccountSnapshot(
            account.AccountId,
            ProviderId,
            account.ExternalUserId,
            account.DisplayName,
            account.SecretReference,
            account.AuthorizedAtUtc);
    }

    public Task<IReadOnlyList<ExternalPlaylistSummary>> GetPlaylistsAsync(
        ExternalAccountId accountId,
        CancellationToken cancellationToken)
    {
        RequireAccount(accountId);
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<ExternalPlaylistSummary>>(playlists.Values
                .OrderBy(playlist => playlist.Name, StringComparer.Ordinal)
                .Select(playlist => new ExternalPlaylistSummary(
                    ProviderId,
                    playlist.ExternalPlaylistId,
                    playlist.Name,
                    playlist.Url,
                    playlist.Items.Count,
                    playlist.LastModifiedAtUtc))
                .ToArray());
        }
    }

    public Task<ExternalPlaylistSnapshot> GetPlaylistAsync(
        ExternalPlaylistRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!StringComparer.Ordinal.Equals(request.ProviderId, ProviderId))
            throw new ArgumentException("Playlist request is for a different provider.", nameof(request));
        if (request.AccountId is { } accountId)
            RequireAccount(accountId);

        lock (gate)
        {
            if (!playlists.TryGetValue(request.ExternalPlaylistId, out var playlist))
                throw new KeyNotFoundException($"Fake playlist '{request.ExternalPlaylistId}' was not found.");

            return Task.FromResult(new ExternalPlaylistSnapshot(
                ProviderId,
                playlist.ExternalPlaylistId,
                playlist.Name,
                playlist.Url,
                playlist.SnapshotVersion,
                playlist.LastModifiedAtUtc,
                playlist.Items
                    .Select((item, index) => item.ToSnapshot(ProviderId, index))
                    .ToArray()));
        }
    }

    public async Task DisconnectAsync(
        ExternalAccountId accountId,
        CancellationToken cancellationToken)
    {
        FakeAccountRecord account;
        lock (gate)
        {
            if (!accounts.Remove(accountId.Value, out account!))
                return;
        }

        await secretStore.DeleteAsync(account.SecretReference, cancellationToken);
    }

    public void UpsertPlaylist(FakePlaylistSeed playlist)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        ArgumentException.ThrowIfNullOrWhiteSpace(playlist.ExternalPlaylistId);
        ArgumentException.ThrowIfNullOrWhiteSpace(playlist.Name);

        lock (gate)
        {
            playlists.TryGetValue(playlist.ExternalPlaylistId, out var existing);
            playlists[playlist.ExternalPlaylistId] = new FakePlaylistRecord(
                playlist.ExternalPlaylistId,
                playlist.Name,
                playlist.Url,
                (existing?.SnapshotVersion ?? 0) + 1,
                DateTimeOffset.UtcNow,
                playlist.Items.ToArray());
        }
    }

    private void RequireAccount(ExternalAccountId accountId)
    {
        lock (gate)
        {
            if (!accounts.ContainsKey(accountId.Value))
                throw new InvalidOperationException("Fake provider account is not connected.");
        }
    }

    private sealed record FakeAccountRecord(
        ExternalAccountId AccountId,
        string ExternalUserId,
        string DisplayName,
        string SecretReference,
        DateTimeOffset AuthorizedAtUtc);

    private sealed record FakePlaylistRecord(
        string ExternalPlaylistId,
        string Name,
        string? Url,
        long SnapshotVersion,
        DateTimeOffset LastModifiedAtUtc,
        IReadOnlyList<FakeTrackSeed> Items);
}

public sealed record FakePlaylistSeed(
    string ExternalPlaylistId,
    string Name,
    string? Url,
    IReadOnlyList<FakeTrackSeed> Items);

public sealed record FakeTrackSeed(
    string ExternalTrackId,
    string ProviderItemId,
    string Title,
    IReadOnlyList<string> Artists,
    string? Album = null,
    int? DurationMs = null,
    string? Isrc = null,
    string? ExternalUrl = null,
    string? ArtworkUrl = null,
    string? MusicBrainzRecordingId = null,
    string RawMetadataJson = "{}")
{
    public ExternalTrackSnapshot ToSnapshot(string providerId, int index)
        => new(
            providerId,
            ExternalTrackId,
            ProviderItemId,
            index,
            Title,
            Artists,
            Album,
            DurationMs,
            Isrc,
            ExternalUrl,
            ArtworkUrl,
            MusicBrainzRecordingId,
            RawMetadataJson);
}
