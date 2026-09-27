using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Integrations.Abstractions;
using Sockseek.Integrations.Bandcamp;

namespace Tests.Application.Providers;

[TestClass]
public sealed class BandcampPlaylistSourceProviderTests
{
    [TestMethod]
    public async Task GetPlaylistAsync_PublicAlbumUrl_MapsFixtureTracklist()
    {
        var handler = new QueueHttpMessageHandler(
        [
            HtmlResponse("""
            <html>
              <head>
                <script type="application/ld+json">
                {
                  "@context": "https://schema.org",
                  "@type": "MusicAlbum",
                  "name": "Fixture Album",
                  "url": "https://artist.bandcamp.com/album/fixture-album",
                  "image": "https://f4.bcbits.com/img/a1.jpg",
                  "byArtist": { "@type": "MusicGroup", "name": "Fixture Artist" },
                  "track": {
                    "@type": "ItemList",
                    "itemListElement": [
                      {
                        "@type": "ListItem",
                        "position": 1,
                        "item": {
                          "@type": "MusicRecording",
                          "name": "First Track",
                          "url": "https://artist.bandcamp.com/track/first-track",
                          "duration": "PT2M30S"
                        }
                      },
                      {
                        "@type": "ListItem",
                        "position": 2,
                        "item": {
                          "@type": "MusicRecording",
                          "name": "Second Track",
                          "url": "https://artist.bandcamp.com/track/second-track",
                          "duration": "PT3M"
                        }
                      }
                    ]
                  }
                }
                </script>
              </head>
            </html>
            """),
        ]);
        var provider = new BandcampPlaylistSourceProvider(new HttpClient(handler));

        var snapshot = await provider.GetPlaylistAsync(new ExternalPlaylistRequest(
            null,
            ProviderIds.Bandcamp,
            "https://artist.bandcamp.com/album/fixture-album",
            "https://artist.bandcamp.com/album/fixture-album"), CancellationToken.None);

        Assert.AreEqual(ProviderIds.Bandcamp, snapshot.ProviderId);
        Assert.AreEqual("Fixture Album", snapshot.Name);
        Assert.AreEqual("https://artist.bandcamp.com/album/fixture-album", snapshot.Url);
        Assert.AreEqual(2, snapshot.Items.Count);
        Assert.AreEqual("https://artist.bandcamp.com/track/first-track", snapshot.Items[0].ExternalTrackId);
        Assert.AreEqual("First Track", snapshot.Items[0].Title);
        CollectionAssert.AreEqual(new[] { "Fixture Artist" }, snapshot.Items[0].Artists.ToArray());
        Assert.AreEqual("Fixture Album", snapshot.Items[0].Album);
        Assert.AreEqual(150000, snapshot.Items[0].DurationMs);
        Assert.AreEqual("https://artist.bandcamp.com/track/first-track", snapshot.Items[0].ExternalUrl);
        Assert.AreEqual("https://f4.bcbits.com/img/a1.jpg", snapshot.Items[0].ArtworkUrl);
        Assert.AreEqual("Second Track", snapshot.Items[1].Title);
        Assert.AreEqual(1, handler.Requests.Count);
        Assert.IsFalse(handler.Requests[0].Headers.Contains("Cookie", StringComparer.OrdinalIgnoreCase));
        Assert.IsFalse(handler.Requests[0].Headers.Contains("Authorization", StringComparer.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task GetPlaylistAsync_PublicTrackUrl_MapsSingleTrackPlaylist()
    {
        var handler = new QueueHttpMessageHandler(
        [
            HtmlResponse("""
            <html>
              <head>
                <script type="application/ld+json">
                {
                  "@context": "https://schema.org",
                  "@type": "MusicRecording",
                  "name": "Standalone Track",
                  "url": "https://artist.bandcamp.com/track/standalone-track",
                  "image": "https://f4.bcbits.com/img/t1.jpg",
                  "duration": "PT4M10S",
                  "byArtist": { "@type": "MusicGroup", "name": "Fixture Artist" }
                }
                </script>
              </head>
            </html>
            """),
        ]);
        var provider = new BandcampPlaylistSourceProvider(new HttpClient(handler));

        var snapshot = await provider.GetPlaylistAsync(new ExternalPlaylistRequest(
            null,
            ProviderIds.Bandcamp,
            "https://artist.bandcamp.com/track/standalone-track",
            "https://artist.bandcamp.com/track/standalone-track"), CancellationToken.None);

        Assert.AreEqual("Standalone Track", snapshot.Name);
        Assert.AreEqual(1, snapshot.Items.Count);
        Assert.AreEqual("Standalone Track", snapshot.Items[0].Title);
        Assert.AreEqual(250000, snapshot.Items[0].DurationMs);
        Assert.AreEqual("https://artist.bandcamp.com/track/standalone-track", snapshot.Items[0].ExternalUrl);
    }

    [TestMethod]
    public async Task GetPlaylistAsync_UnsupportedBandcampUrl_IsRejectedBeforeNetwork()
    {
        var handler = new QueueHttpMessageHandler([]);
        var provider = new BandcampPlaylistSourceProvider(new HttpClient(handler));

        var exception = await Assert.ThrowsExceptionAsync<BandcampProviderException>(
            () => provider.GetPlaylistAsync(new ExternalPlaylistRequest(
                null,
                ProviderIds.Bandcamp,
                "https://artist.bandcamp.com/merch/shirt",
                "https://artist.bandcamp.com/merch/shirt"), CancellationToken.None));

        StringAssert.Contains(exception.Message, "public album and track URLs");
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetPlaylistAsync_NonBandcampUrl_IsRejectedBeforeNetwork()
    {
        var handler = new QueueHttpMessageHandler([]);
        var provider = new BandcampPlaylistSourceProvider(new HttpClient(handler));

        var exception = await Assert.ThrowsExceptionAsync<BandcampProviderException>(
            () => provider.GetPlaylistAsync(new ExternalPlaylistRequest(
                null,
                ProviderIds.Bandcamp,
                "https://example.com/album/not-bandcamp",
                "https://example.com/album/not-bandcamp"), CancellationToken.None));

        StringAssert.Contains(exception.Message, "bandcamp.com");
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task GetPlaylistAsync_ParserFailure_IsLocalizedToImport()
    {
        var handler = new QueueHttpMessageHandler(
        [
            HtmlResponse("<html><head></head><body>No structured data</body></html>"),
        ]);
        var provider = new BandcampPlaylistSourceProvider(new HttpClient(handler));

        var exception = await Assert.ThrowsExceptionAsync<BandcampProviderException>(
            () => provider.GetPlaylistAsync(new ExternalPlaylistRequest(
                null,
                ProviderIds.Bandcamp,
                "https://artist.bandcamp.com/album/missing-data",
                "https://artist.bandcamp.com/album/missing-data"), CancellationToken.None));

        StringAssert.Contains(exception.Message, "structured metadata");
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public void BandcampProvider_DoesNotExposeAccountOrPlaybackCapabilities()
    {
        var provider = new BandcampPlaylistSourceProvider(new HttpClient(new QueueHttpMessageHandler([])));
        var publicMembers = typeof(BandcampPlaylistSourceProvider)
            .GetMethods()
            .Select(method => method.Name)
            .Concat(typeof(IPlaylistSourceProvider).GetMethods().Select(method => method.Name))
            .ToArray();

        Assert.IsTrue(provider.Capabilities.HasFlag(PlaylistProviderCapabilities.ImportPublicUrl));
        Assert.IsFalse(provider.Capabilities.HasFlag(PlaylistProviderCapabilities.ConnectAccount));
        CollectionAssert.DoesNotContain(publicMembers, "GetAudioStreamAsync");
        CollectionAssert.DoesNotContain(publicMembers, "DownloadTrackAsync");
        CollectionAssert.DoesNotContain(publicMembers, "PlayAsync");
        CollectionAssert.DoesNotContain(publicMembers, "GetPlaybackUrlAsync");
    }

    private static HttpResponseMessage HtmlResponse(string html)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html"),
        };

    private sealed class QueueHttpMessageHandler(IReadOnlyList<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private int index;

        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri ?? new Uri("about:blank"),
                request.Method,
                request.Headers.Select(header => header.Key).ToArray()));

            if (index >= responses.Count)
                throw new InvalidOperationException("No queued Bandcamp fixture response is available.");

            return Task.FromResult(responses[index++]);
        }
    }

    private sealed record RecordedRequest(
        Uri Uri,
        HttpMethod Method,
        IReadOnlyList<string> Headers);
}
