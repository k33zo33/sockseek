namespace Sockseek.Integrations.Abstractions;

public sealed record ProviderCapabilities(
    bool SupportsPlaylistImport,
    bool SupportsMetadataLookup,
    bool SupportsAccountConnection,
    bool SupportsPublicUrlImport);
