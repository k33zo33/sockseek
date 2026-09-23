using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class ProviderConnectionCardViewModelTests
{
    [TestMethod]
    public void FromCapability_HidesConnectAccountForBandcamp()
    {
        var capability = new ProviderCapabilityDto(
            "bandcamp",
            "Bandcamp",
            SupportsPlaylistImport: true,
            SupportsMetadataLookup: false,
            SupportsAccountConnection: false,
            SupportsPublicUrlImport: true,
            ["ImportPublicUrl", "ReadPlaylistItems"]);

        var card = ProviderConnectionCardViewModel.FromCapability(capability);

        Assert.AreEqual("bandcamp", card.ProviderId);
        Assert.IsFalse(card.CanConnectAccount);
        Assert.IsTrue(card.CanImportPublicUrl);
        Assert.AreEqual(ProviderConnectionPrimaryAction.ImportPublicUrl, card.PrimaryAction);
        CollectionAssert.DoesNotContain(card.Capabilities.ToArray(), "ConnectAccount");
    }

    [TestMethod]
    public void FromCapability_ShowsConnectAccountForSpotify()
    {
        var capability = new ProviderCapabilityDto(
            "spotify",
            "Spotify",
            SupportsPlaylistImport: true,
            SupportsMetadataLookup: false,
            SupportsAccountConnection: true,
            SupportsPublicUrlImport: false,
            ["ConnectAccount", "ListUserPlaylists", "ReadPlaylistItems"]);

        var card = ProviderConnectionCardViewModel.FromCapability(capability);

        Assert.IsTrue(card.CanConnectAccount);
        Assert.IsFalse(card.CanImportPublicUrl);
        Assert.AreEqual(ProviderConnectionPrimaryAction.ConnectAccount, card.PrimaryAction);
    }
}
