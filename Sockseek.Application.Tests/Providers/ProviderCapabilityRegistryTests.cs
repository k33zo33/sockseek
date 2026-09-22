using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Providers;
using Sockseek.Integrations.Abstractions;

namespace Tests.Application.Providers;

[TestClass]
public sealed class ProviderCapabilityRegistryTests
{
    [TestMethod]
    public void DefaultRegistry_HidesBandcampAccountConnection()
    {
        var registry = ProviderCapabilityRegistry.CreateDefault();

        var bandcamp = registry.Find(ProviderIds.Bandcamp);

        Assert.IsNotNull(bandcamp);
        Assert.IsTrue(bandcamp.SupportsPublicUrlImport);
        Assert.IsFalse(bandcamp.SupportsAccountConnection);
        Assert.IsFalse(registry.Supports(ProviderIds.Bandcamp, PlaylistProviderCapabilities.ConnectAccount));
    }

    [TestMethod]
    public void DefaultRegistry_ExposesAccountConnectionOnlyForAccountProviders()
    {
        var registry = ProviderCapabilityRegistry.CreateDefault();

        Assert.IsTrue(registry.Supports(ProviderIds.Spotify, PlaylistProviderCapabilities.ConnectAccount));
        Assert.IsTrue(registry.Supports(ProviderIds.YouTube, PlaylistProviderCapabilities.ConnectAccount));
        Assert.IsFalse(registry.Supports(ProviderIds.MusicBrainz, PlaylistProviderCapabilities.ConnectAccount));
        Assert.IsNull(registry.Find(ProviderIds.Fake));
    }

    [TestMethod]
    public void TestRegistry_CanIncludeFakeProvider()
    {
        var registry = new ProviderCapabilityRegistry(DefaultProviderCapabilities.WithFake);

        Assert.IsTrue(registry.Supports(ProviderIds.Fake, PlaylistProviderCapabilities.ConnectAccount));
        Assert.IsTrue(registry.Supports(ProviderIds.Fake, PlaylistProviderCapabilities.IncrementalSync));
    }

    [TestMethod]
    public void DefaultRegistry_ReportsMusicBrainzAsMetadataOnly()
    {
        var registry = ProviderCapabilityRegistry.CreateDefault();

        var musicBrainz = registry.Find(ProviderIds.MusicBrainz);

        Assert.IsNotNull(musicBrainz);
        Assert.IsTrue(musicBrainz.SupportsMetadataLookup);
        Assert.IsFalse(musicBrainz.SupportsPlaylistImport);
        Assert.IsFalse(musicBrainz.SupportsAccountConnection);
    }

    [TestMethod]
    public void ProviderIds_AreUnique()
    {
        var providerIds = ProviderCapabilityRegistry.CreateDefault()
            .List()
            .Select(provider => provider.ProviderId)
            .ToArray();

        CollectionAssert.AreEquivalent(
            providerIds,
            providerIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    [TestMethod]
    public void PlaylistProviderContract_DoesNotExposeAudioOrDownloadMethods()
    {
        var methodNames = typeof(IPlaylistSourceProvider)
            .GetMethods()
            .Select(method => method.Name)
            .ToArray();

        CollectionAssert.DoesNotContain(methodNames, "GetAudioStreamAsync");
        CollectionAssert.DoesNotContain(methodNames, "DownloadTrackAsync");
        Assert.IsFalse(methodNames.Any(name => name.Contains("Audio", StringComparison.OrdinalIgnoreCase)));
    }
}
