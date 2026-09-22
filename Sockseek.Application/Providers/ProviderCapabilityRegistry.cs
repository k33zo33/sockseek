using Sockseek.Integrations.Abstractions;

namespace Sockseek.Application.Providers;

public sealed class ProviderCapabilityRegistry
{
    private readonly IReadOnlyDictionary<string, ProviderCapabilities> providers;

    public ProviderCapabilityRegistry(IEnumerable<ProviderCapabilities> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var byId = new Dictionary<string, ProviderCapabilities>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            if (string.IsNullOrWhiteSpace(provider.ProviderId))
                throw new ArgumentException("Provider id is required.", nameof(providers));
            if (!byId.TryAdd(provider.ProviderId, provider))
                throw new ArgumentException($"Duplicate provider id '{provider.ProviderId}'.", nameof(providers));
        }

        this.providers = byId;
    }

    public static ProviderCapabilityRegistry CreateDefault()
        => new(DefaultProviderCapabilities.All);

    public IReadOnlyList<ProviderCapabilities> List()
        => providers.Values
            .OrderBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public ProviderCapabilities? Find(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        return providers.TryGetValue(providerId, out var provider) ? provider : null;
    }

    public bool Supports(string providerId, PlaylistProviderCapabilities capability)
        => Find(providerId)?.Supports(capability) == true;
}

public static class DefaultProviderCapabilities
{
    public static IReadOnlyList<ProviderCapabilities> All => Production;

    public static IReadOnlyList<ProviderCapabilities> Production { get; } =
    [
        new(
            ProviderIds.Spotify,
            "Spotify",
            PlaylistProviderCapabilities.ConnectAccount
            | PlaylistProviderCapabilities.ListUserPlaylists
            | PlaylistProviderCapabilities.ReadPlaylistItems
            | PlaylistProviderCapabilities.ReadSavedTracks
            | PlaylistProviderCapabilities.IncrementalSync
            | PlaylistProviderCapabilities.RequiresManualAppApproval),
        new(
            ProviderIds.YouTube,
            "YouTube",
            PlaylistProviderCapabilities.ConnectAccount
            | PlaylistProviderCapabilities.ListUserPlaylists
            | PlaylistProviderCapabilities.ReadPlaylistItems
            | PlaylistProviderCapabilities.IncrementalSync
            | PlaylistProviderCapabilities.RequiresManualAppApproval),
        new(
            ProviderIds.Bandcamp,
            "Bandcamp",
            PlaylistProviderCapabilities.ImportPublicUrl
            | PlaylistProviderCapabilities.ReadPlaylistItems),
        new(
            ProviderIds.MusicBrainz,
            "MusicBrainz",
            PlaylistProviderCapabilities.LookupMetadata),
    ];

    public static IReadOnlyList<ProviderCapabilities> WithFake { get; } =
    [
        .. Production,
        new(
            ProviderIds.Fake,
            "Fake Provider",
            PlaylistProviderCapabilities.ConnectAccount
            | PlaylistProviderCapabilities.ListUserPlaylists
            | PlaylistProviderCapabilities.ReadPlaylistItems
            | PlaylistProviderCapabilities.IncrementalSync),
    ];
}
