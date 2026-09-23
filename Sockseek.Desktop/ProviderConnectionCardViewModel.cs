using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class ProviderConnectionCardViewModel
{
    private ProviderConnectionCardViewModel(ProviderCapabilityDto capability)
    {
        ProviderId = capability.ProviderId;
        DisplayName = capability.DisplayName;
        CanConnectAccount = capability.SupportsAccountConnection;
        CanImportPublicUrl = capability.SupportsPublicUrlImport;
        CanImportPlaylists = capability.SupportsPlaylistImport;
        CanLookupMetadata = capability.SupportsMetadataLookup;
        Capabilities = capability.Capabilities;
        PrimaryAction = CanConnectAccount
            ? ProviderConnectionPrimaryAction.ConnectAccount
            : CanImportPublicUrl
                ? ProviderConnectionPrimaryAction.ImportPublicUrl
                : CanLookupMetadata
                    ? ProviderConnectionPrimaryAction.LookupMetadata
                    : ProviderConnectionPrimaryAction.None;
    }

    public string ProviderId { get; }

    public string DisplayName { get; }

    public bool CanConnectAccount { get; }

    public bool CanImportPublicUrl { get; }

    public bool CanImportPlaylists { get; }

    public bool CanLookupMetadata { get; }

    public ProviderConnectionPrimaryAction PrimaryAction { get; }

    public IReadOnlyList<string> Capabilities { get; }

    public static ProviderConnectionCardViewModel FromCapability(ProviderCapabilityDto capability)
        => new(capability ?? throw new ArgumentNullException(nameof(capability)));
}

public enum ProviderConnectionPrimaryAction
{
    None,
    ConnectAccount,
    ImportPublicUrl,
    LookupMetadata,
}
