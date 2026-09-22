namespace Sockseek.Integrations.Abstractions;

[Flags]
public enum PlaylistProviderCapabilities
{
    None = 0,
    ConnectAccount = 1 << 0,
    ImportPublicUrl = 1 << 1,
    ListUserPlaylists = 1 << 2,
    ReadPlaylistItems = 1 << 3,
    ReadSavedTracks = 1 << 4,
    IncrementalSync = 1 << 5,
    RequiresManualAppApproval = 1 << 6,
    LookupMetadata = 1 << 7,
}

public static class ProviderIds
{
    public const string Spotify = "spotify";
    public const string YouTube = "youtube";
    public const string Bandcamp = "bandcamp";
    public const string MusicBrainz = "musicbrainz";
    public const string Fake = "fake";
}

public sealed record ProviderCapabilities(
    string ProviderId,
    string DisplayName,
    PlaylistProviderCapabilities Capabilities)
{
    public bool SupportsPlaylistImport => Supports(PlaylistProviderCapabilities.ListUserPlaylists)
        || Supports(PlaylistProviderCapabilities.ImportPublicUrl);

    public bool SupportsMetadataLookup => Supports(PlaylistProviderCapabilities.LookupMetadata);

    public bool SupportsAccountConnection => Supports(PlaylistProviderCapabilities.ConnectAccount);

    public bool SupportsPublicUrlImport => Supports(PlaylistProviderCapabilities.ImportPublicUrl);

    public bool Supports(PlaylistProviderCapabilities capability)
        => (Capabilities & capability) == capability;
}

public interface IPlaylistSourceProvider
{
    string ProviderId { get; }
    PlaylistProviderCapabilities Capabilities { get; }

    Task<AuthorizationStartResult> StartAuthorizationAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken);

    Task<ExternalAccountSnapshot> CompleteAuthorizationAsync(
        AuthorizationCallback callback,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalPlaylistSummary>> GetPlaylistsAsync(
        ExternalAccountId accountId,
        CancellationToken cancellationToken);

    Task<ExternalPlaylistSnapshot> GetPlaylistAsync(
        ExternalPlaylistRequest request,
        CancellationToken cancellationToken);

    Task DisconnectAsync(
        ExternalAccountId accountId,
        CancellationToken cancellationToken);
}

public sealed record ExternalAccountId(Guid Value);

public sealed record AuthorizationRequest(
    string ProviderId,
    Uri RedirectUri,
    IReadOnlyList<string> Scopes,
    string State,
    string CodeChallenge,
    string CodeChallengeMethod);

public sealed record AuthorizationStartResult(
    Uri AuthorizationUri,
    string State);

public sealed record AuthorizationCallback(
    string ProviderId,
    Uri RedirectUri,
    string State,
    string? Code,
    string? Error);

public sealed record ExternalAccountSnapshot(
    ExternalAccountId AccountId,
    string ProviderId,
    string ExternalUserId,
    string DisplayName,
    string SecretReference,
    DateTimeOffset AuthorizedAtUtc);

public sealed record ExternalPlaylistSummary(
    string ProviderId,
    string ExternalPlaylistId,
    string Name,
    string? Url,
    int? ItemCount,
    DateTimeOffset? LastModifiedAtUtc);

public sealed record ExternalPlaylistRequest(
    ExternalAccountId? AccountId,
    string ProviderId,
    string ExternalPlaylistId,
    string? Url);

public sealed record ExternalPlaylistSnapshot(
    string ProviderId,
    string ExternalPlaylistId,
    string Name,
    string? Url,
    long SnapshotVersion,
    DateTimeOffset SyncedAtUtc,
    IReadOnlyList<ExternalTrackSnapshot> Items);

public sealed record ExternalTrackSnapshot(
    string ProviderId,
    string ExternalTrackId,
    string ProviderItemId,
    int Position,
    string Title,
    IReadOnlyList<string> Artists,
    string? Album,
    int? DurationMs,
    string? Isrc,
    string? ExternalUrl,
    string? ArtworkUrl,
    string? MusicBrainzRecordingId,
    string RawMetadataJson);
