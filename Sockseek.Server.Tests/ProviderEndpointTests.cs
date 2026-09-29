using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;
using Sockseek.Infrastructure.Security;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class ProviderEndpointTests
{
    [TestMethod]
    public async Task GetProviders_ReturnsCapabilityDrivenProviderList()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var providers = await client.GetProvidersAsync();
            var bandcamp = providers.Single(provider => provider.ProviderId == "bandcamp");
            var spotify = providers.Single(provider => provider.ProviderId == "spotify");
            var musicBrainz = providers.Single(provider => provider.ProviderId == "musicbrainz");

            Assert.IsTrue(spotify.SupportsAccountConnection);
            Assert.IsTrue(spotify.Capabilities.Contains("ConnectAccount"));
            Assert.IsTrue(bandcamp.SupportsPublicUrlImport);
            Assert.IsFalse(bandcamp.SupportsAccountConnection);
            CollectionAssert.DoesNotContain(bandcamp.Capabilities.ToArray(), "ConnectAccount");
            Assert.IsTrue(musicBrainz.SupportsMetadataLookup);
            Assert.IsFalse(musicBrainz.SupportsPlaylistImport);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetProviderCapabilities_ReturnsSingleProviderOrNotFound()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var provider = await client.GetProviderCapabilitiesAsync("youtube");
            var missing = await client.GetProviderCapabilitiesAsync("missing-provider");

            Assert.IsNotNull(provider);
            Assert.AreEqual("youtube", provider.ProviderId);
            Assert.IsTrue(provider.SupportsAccountConnection);
            Assert.IsNull(missing);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetProviders_RequiresSessionToken()
    {
        var app = CreateApp(out var url, out _, out var tempRoot);
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(url) };

            using var response = await http.GetAsync("/api/v1/providers");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetExternalAccounts_ReturnsAccountStatusWithoutSecretReference()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var accountId = await SeedExternalAccountAsync(app);
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var accounts = await client.GetExternalAccountsAsync();
            var rawJson = await http.GetStringAsync("api/v1/accounts");

            var account = accounts.Single();
            Assert.AreEqual(accountId, account.AccountId);
            Assert.AreEqual("spotify", account.ProviderId);
            Assert.AreEqual("user-1", account.ExternalUserId);
            Assert.AreEqual("Alice", account.DisplayName);
            Assert.AreEqual("Authorized", account.Status);
            Assert.IsFalse(rawJson.Contains("secretReference", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(rawJson.Contains("secret://", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(rawJson.Contains("access-token", StringComparison.Ordinal));
            Assert.IsFalse(rawJson.Contains("refresh-token", StringComparison.Ordinal));
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task DisconnectExternalAccount_ClearsSecretReferenceAndReturnsDisconnectedStatus()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var accountId = await SeedExternalAccountAsync(app);
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var disconnected = await client.DisconnectExternalAccountAsync(accountId);

            Assert.IsNotNull(disconnected);
            Assert.AreEqual(accountId, disconnected.AccountId);
            Assert.AreEqual("Disconnected", disconnected.Status);
            await using var verifyScope = app.Services.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            var account = await verifyDb.ExternalAccounts.SingleAsync(entity => entity.Id == accountId);
            Assert.AreEqual((int)ExternalAccountStatus.Disconnected, account.Status);
            Assert.AreEqual(string.Empty, account.SecretReference);
            Assert.AreEqual(1, await verifyDb.ExternalPlaylists.CountAsync(playlist => playlist.AccountId == accountId));
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task DisconnectExternalAccount_ReturnsNotFoundForMissingAccount()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var disconnected = await client.DisconnectExternalAccountAsync(Guid.NewGuid());

            Assert.IsNull(disconnected);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetExternalAccounts_RequiresSessionToken()
    {
        var app = CreateApp(out var url, out _, out var tempRoot);
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(url) };

            using var response = await http.GetAsync("/api/v1/accounts");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task SpotifyAuthorizationListAndMirrorImport_UsesFixtureHttpAndPreservesLocalSnapshot()
    {
        var spotify = new QueueSpotifyHandler(
        [
            JsonResponse("""
            {
              "access_token": "access-1",
              "refresh_token": "refresh-1",
              "token_type": "Bearer",
              "expires_in": 3600,
              "scope": "playlist-read-private playlist-read-collaborative"
            }
            """),
            JsonResponse("""{ "id": "spotify-user-1", "display_name": "Spotify User" }"""),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "playlist-1",
                  "name": "Spotify Mix",
                  "external_urls": { "spotify": "https://open.spotify.com/playlist/playlist-1" },
                  "tracks": { "total": 2 }
                }
              ],
              "next": null
            }
            """),
            JsonResponse("""
            {
              "id": "playlist-1",
              "name": "Spotify Mix",
              "snapshot_id": "snapshot-1",
              "external_urls": { "spotify": "https://open.spotify.com/playlist/playlist-1" }
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "track": {
                    "type": "track",
                    "id": "track-1",
                    "uri": "spotify:track:track-1",
                    "name": "First Track",
                    "duration_ms": 180000,
                    "external_urls": { "spotify": "https://open.spotify.com/track/track-1" },
                    "external_ids": { "isrc": "USRC17607839" },
                    "artists": [{ "name": "Artist One" }],
                    "album": {
                      "name": "Album One",
                      "images": [{ "url": "https://i.scdn.co/image/one", "width": 640, "height": 640 }]
                    }
                  }
                },
                {
                  "track": {
                    "type": "track",
                    "id": "track-2",
                    "uri": "spotify:track:track-2",
                    "name": "Second Track",
                    "duration_ms": 181000,
                    "artists": [{ "name": "Artist Two" }],
                    "album": { "name": "Album Two", "images": [] }
                  }
                }
              ],
              "next": null
            }
            """),
            JsonResponse("""
            {
              "id": "playlist-1",
              "name": "Spotify Mix",
              "snapshot_id": "snapshot-2",
              "external_urls": { "spotify": "https://open.spotify.com/playlist/playlist-1" }
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "track": {
                    "type": "track",
                    "id": "track-1",
                    "uri": "spotify:track:track-1",
                    "name": "First Track",
                    "duration_ms": 180000,
                    "external_urls": { "spotify": "https://open.spotify.com/track/track-1" },
                    "external_ids": { "isrc": "USRC17607839" },
                    "artists": [{ "name": "Artist One" }],
                    "album": {
                      "name": "Album One",
                      "images": [{ "url": "https://i.scdn.co/image/one", "width": 640, "height": 640 }]
                    }
                  }
                }
              ],
              "next": null
            }
            """),
        ]);
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot, spotifyHandler: spotify);
        await app.StartAsync();
        try
        {
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var start = await client.StartProviderAuthorizationAsync(
                "spotify",
                new ProviderAuthorizationStartRequestDto("http://127.0.0.1:49152/callback"));
            var account = await client.CompleteProviderAuthorizationAsync(
                "spotify",
                new ProviderAuthorizationCallbackRequestDto(
                    "http://127.0.0.1:49152/callback",
                    start.State,
                    "code-1"));
            var playlists = await client.GetProviderPlaylistsAsync(account.AccountId);
            var firstImport = await client.ImportProviderPlaylistAsync(
                account.AccountId,
                "playlist-1",
                new ImportProviderPlaylistRequestDto("Mirror"));
            var secondImport = await client.ImportProviderPlaylistAsync(
                account.AccountId,
                "playlist-1",
                new ImportProviderPlaylistRequestDto("Mirror"));

            Assert.AreEqual("spotify", start.ProviderId);
            StringAssert.Contains(start.AuthorizationUri, "playlist-read-private");
            StringAssert.Contains(start.AuthorizationUri, "playlist-read-collaborative");
            Assert.IsFalse(start.AuthorizationUri.Contains("streaming", StringComparison.Ordinal));
            Assert.AreEqual("spotify-user-1", account.ExternalUserId);
            Assert.AreEqual(1, playlists.Count);
            Assert.AreEqual("Spotify Mix", playlists.Single().Name);
            Assert.AreEqual(firstImport.PlaylistId, secondImport.PlaylistId);
            Assert.AreEqual(2, firstImport.ItemCount);
            Assert.AreEqual(1, secondImport.ItemCount);
            StringAssert.Contains(spotify.Requests[0].Body!, "code_verifier=");
            var rawAccounts = await http.GetStringAsync("api/v1/accounts");
            Assert.IsFalse(rawAccounts.Contains("access-1", StringComparison.Ordinal));
            Assert.IsFalse(rawAccounts.Contains("refresh-1", StringComparison.Ordinal));

            await using var verifyScope = app.Services.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            Assert.AreEqual(1, await db.ExternalAccounts.CountAsync());
            Assert.AreEqual(1, await db.ExternalPlaylists.CountAsync());
            Assert.AreEqual(1, await db.Playlists.CountAsync());
            Assert.AreEqual(2, await db.PlaylistItems.CountAsync());
            var firstItem = await db.PlaylistItems.SingleAsync(item => item.ProviderItemId == "spotify:track:track-1");
            var removedItem = await db.PlaylistItems.SingleAsync(item => item.ProviderItemId == "spotify:track:track-2");
            StringAssert.Contains(firstItem.SnapshotJson, "\"Isrc\":\"USRC17607839\"");
            StringAssert.Contains(firstItem.SnapshotJson, "\"ExternalUrl\":\"https://open.spotify.com/track/track-1\"");
            StringAssert.Contains(firstItem.SnapshotJson, "\"ArtworkUrl\":\"https://i.scdn.co/image/one\"");
            Assert.AreEqual((int)PlaylistItemStatus.RemovedFromSourcePlaylist, removedItem.Status);
            Assert.IsNotNull(removedItem.RemovedAtUtc);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetProviderPlaylists_WhenSpotifyForbidden_ReturnsDevelopmentModeMessage()
    {
        var spotify = new QueueSpotifyHandler(
        [
            JsonResponse("""
            {
              "access_token": "access-1",
              "refresh_token": "refresh-1",
              "token_type": "Bearer",
              "expires_in": 3600,
              "scope": "playlist-read-private playlist-read-collaborative"
            }
            """),
            JsonResponse("""{ "id": "spotify-user-1", "display_name": "Spotify User" }"""),
            JsonResponse("""{ "error": { "status": 403, "message": "User not registered in the Developer Dashboard" } }""", HttpStatusCode.Forbidden),
        ]);
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot, spotifyHandler: spotify);
        await app.StartAsync();
        try
        {
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var start = await client.StartProviderAuthorizationAsync(
                "spotify",
                new ProviderAuthorizationStartRequestDto("http://127.0.0.1:49152/callback"));
            var account = await client.CompleteProviderAuthorizationAsync(
                "spotify",
                new ProviderAuthorizationCallbackRequestDto(
                    "http://127.0.0.1:49152/callback",
                    start.State,
                    "code-1"));

            using var response = await http.GetAsync($"api/v1/accounts/{account.AccountId}/provider-playlists");
            var body = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
            StringAssert.Contains(body, "provider_forbidden");
            StringAssert.Contains(body, "development-mode");
            StringAssert.Contains(body, "allowlist");
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task YouTubeAuthorizationListAndMirrorImport_UsesFixtureHttpAndPreservesLocalSnapshot()
    {
        var youtube = new QueueYouTubeHandler(
        [
            JsonResponse("""
            {
              "access_token": "access-1",
              "refresh_token": "refresh-1",
              "token_type": "Bearer",
              "expires_in": 3600,
              "scope": "https://www.googleapis.com/auth/youtube.readonly"
            }
            """),
            JsonResponse("""
            {
              "items": [
                { "id": "channel-1", "snippet": { "title": "Channel One" } }
              ]
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "playlist-1",
                  "snippet": { "title": "YouTube Mix", "publishedAt": "2026-01-01T00:00:00Z" },
                  "contentDetails": { "itemCount": 2 }
                }
              ]
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "playlist-1",
                  "snippet": { "title": "YouTube Mix", "publishedAt": "2026-01-01T00:00:00Z" },
                  "contentDetails": { "itemCount": 2 }
                }
              ]
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "playlist-item-1",
                  "snippet": {
                    "title": "First Video",
                    "position": 0,
                    "videoOwnerChannelTitle": "Video Channel",
                    "resourceId": { "kind": "youtube#video", "videoId": "video-1" },
                    "thumbnails": { "high": { "url": "https://img.youtube.test/one.jpg" } }
                  },
                  "contentDetails": { "videoId": "video-1" },
                  "status": { "privacyStatus": "public" }
                },
                {
                  "id": "playlist-item-2",
                  "snippet": {
                    "title": "Private video",
                    "position": 1,
                    "resourceId": { "kind": "youtube#video", "videoId": "private-video" }
                  },
                  "contentDetails": { "videoId": "private-video" },
                  "status": { "privacyStatus": "private" }
                }
              ]
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "video-1",
                  "snippet": { "title": "First Video From Lookup", "channelTitle": "Video Channel" },
                  "contentDetails": { "duration": "PT3M" },
                  "status": { "privacyStatus": "public" }
                }
              ]
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "playlist-1",
                  "snippet": { "title": "YouTube Mix", "publishedAt": "2026-01-02T00:00:00Z" },
                  "contentDetails": { "itemCount": 1 }
                }
              ]
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "playlist-item-1",
                  "snippet": {
                    "title": "First Video",
                    "position": 0,
                    "videoOwnerChannelTitle": "Video Channel",
                    "resourceId": { "kind": "youtube#video", "videoId": "video-1" },
                    "thumbnails": { "high": { "url": "https://img.youtube.test/one.jpg" } }
                  },
                  "contentDetails": { "videoId": "video-1" },
                  "status": { "privacyStatus": "public" }
                }
              ]
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "video-1",
                  "snippet": { "title": "First Video From Lookup", "channelTitle": "Video Channel" },
                  "contentDetails": { "duration": "PT3M" },
                  "status": { "privacyStatus": "public" }
                }
              ]
            }
            """),
        ]);
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot, youtubeHandler: youtube);
        await app.StartAsync();
        try
        {
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var start = await client.StartProviderAuthorizationAsync(
                "youtube",
                new ProviderAuthorizationStartRequestDto("http://127.0.0.1:49152/callback"));
            var account = await client.CompleteProviderAuthorizationAsync(
                "youtube",
                new ProviderAuthorizationCallbackRequestDto(
                    "http://127.0.0.1:49152/callback",
                    start.State,
                    "code-1"));
            var playlists = await client.GetProviderPlaylistsAsync(account.AccountId);
            var firstImport = await client.ImportProviderPlaylistAsync(
                account.AccountId,
                "playlist-1",
                new ImportProviderPlaylistRequestDto("Mirror"));
            var secondImport = await client.ImportProviderPlaylistAsync(
                account.AccountId,
                "playlist-1",
                new ImportProviderPlaylistRequestDto("Mirror"));

            Assert.AreEqual("youtube", start.ProviderId);
            StringAssert.Contains(start.AuthorizationUri, "youtube.readonly");
            Assert.IsFalse(start.AuthorizationUri.Contains("streaming", StringComparison.Ordinal));
            Assert.IsFalse(start.AuthorizationUri.Contains("download", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual("channel-1", account.ExternalUserId);
            Assert.AreEqual(1, playlists.Count);
            Assert.AreEqual("YouTube Mix", playlists.Single().Name);
            Assert.AreEqual(firstImport.PlaylistId, secondImport.PlaylistId);
            Assert.AreEqual(2, firstImport.ItemCount);
            Assert.AreEqual(1, secondImport.ItemCount);
            StringAssert.Contains(youtube.Requests[0].Body!, "code_verifier=");

            var rawAccounts = await http.GetStringAsync("api/v1/accounts");
            Assert.IsFalse(rawAccounts.Contains("access-1", StringComparison.Ordinal));
            Assert.IsFalse(rawAccounts.Contains("refresh-1", StringComparison.Ordinal));

            await using var verifyScope = app.Services.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            Assert.AreEqual(1, await db.ExternalAccounts.CountAsync());
            Assert.AreEqual(1, await db.ExternalPlaylists.CountAsync());
            Assert.AreEqual(1, await db.Playlists.CountAsync());
            Assert.AreEqual(2, await db.PlaylistItems.CountAsync());
            var firstItem = await db.PlaylistItems.SingleAsync(item => item.ProviderItemId == "playlist-item-1");
            var removedItem = await db.PlaylistItems.SingleAsync(item => item.ProviderItemId == "playlist-item-2");
            StringAssert.Contains(firstItem.SnapshotJson, "\"ExternalTrackId\":\"video-1\"");
            StringAssert.Contains(firstItem.SnapshotJson, "\"ExternalUrl\":\"https://www.youtube.com/watch?v=video-1\"");
            StringAssert.Contains(firstItem.SnapshotJson, "\"ArtworkUrl\":\"https://img.youtube.test/one.jpg\"");
            Assert.AreEqual((int)PlaylistItemStatus.RemovedFromSourcePlaylist, removedItem.Status);
            Assert.IsNotNull(removedItem.RemovedAtUtc);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetProviderPlaylists_WhenYouTubeRefreshRevoked_MarksAccountAuthorizationExpired()
    {
        var youtube = new QueueYouTubeHandler(
        [
            JsonResponse("""
            {
              "access_token": "expired-access",
              "refresh_token": "refresh-1",
              "token_type": "Bearer",
              "expires_in": 3600,
              "scope": "https://www.googleapis.com/auth/youtube.readonly"
            }
            """),
            JsonResponse("""
            {
              "items": [
                { "id": "channel-1", "snippet": { "title": "Channel One" } }
              ]
            }
            """),
            JsonResponse("""{ "error": { "code": 401, "message": "Invalid Credentials" } }""", HttpStatusCode.Unauthorized),
            JsonResponse("""{ "error": "invalid_grant", "error_description": "Token has been expired or revoked." }""", HttpStatusCode.BadRequest),
        ]);
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot, youtubeHandler: youtube);
        await app.StartAsync();
        try
        {
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var start = await client.StartProviderAuthorizationAsync(
                "youtube",
                new ProviderAuthorizationStartRequestDto("http://127.0.0.1:49152/callback"));
            var account = await client.CompleteProviderAuthorizationAsync(
                "youtube",
                new ProviderAuthorizationCallbackRequestDto(
                    "http://127.0.0.1:49152/callback",
                    start.State,
                    "code-1"));

            using var response = await http.GetAsync($"api/v1/accounts/{account.AccountId}/provider-playlists");
            var body = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            StringAssert.Contains(body, "provider_reauthorization_required");
            StringAssert.Contains(body, "Reconnect YouTube");

            await using var verifyScope = app.Services.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            var stored = await db.ExternalAccounts.SingleAsync(entity => entity.Id == account.AccountId);
            Assert.AreEqual((int)ExternalAccountStatus.AuthorizationExpired, stored.Status);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task ImportProviderPublicUrl_BandcampAlbum_ImportsLocalPlaylistWithoutAccount()
    {
        var bandcamp = new QueueBandcampHandler(
        [
            HtmlResponse("""
            <html>
              <head>
                <script type="application/ld+json">
                {
                  "@context": "https://schema.org",
                  "@type": "MusicAlbum",
                  "name": "Bandcamp Fixture",
                  "url": "https://artist.bandcamp.com/album/bandcamp-fixture",
                  "image": "https://f4.bcbits.com/img/a1.jpg",
                  "byArtist": { "@type": "MusicGroup", "name": "Bandcamp Artist" },
                  "track": {
                    "@type": "ItemList",
                    "itemListElement": [
                      {
                        "@type": "ListItem",
                        "item": {
                          "@type": "MusicRecording",
                          "name": "First Bandcamp Track",
                          "url": "https://artist.bandcamp.com/track/first-bandcamp-track",
                          "duration": "PT2M"
                        }
                      },
                      {
                        "@type": "ListItem",
                        "item": {
                          "@type": "MusicRecording",
                          "name": "Second Bandcamp Track",
                          "url": "https://artist.bandcamp.com/track/second-bandcamp-track",
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
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot, bandcampHandler: bandcamp);
        await app.StartAsync();
        try
        {
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var imported = await client.ImportProviderPublicUrlAsync(
                "bandcamp",
                new ImportProviderPublicUrlRequestDto(
                    "https://artist.bandcamp.com/album/bandcamp-fixture",
                    "Copy"));

            Assert.AreEqual("bandcamp", imported.ProviderId);
            Assert.AreEqual("Bandcamp Fixture", imported.Name);
            Assert.AreEqual(2, imported.ItemCount);
            Assert.AreEqual(1, bandcamp.Requests.Count);
            CollectionAssert.DoesNotContain(bandcamp.Requests[0].Headers.ToArray(), "Cookie");
            CollectionAssert.DoesNotContain(bandcamp.Requests[0].Headers.ToArray(), "Authorization");

            await using var verifyScope = app.Services.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            Assert.AreEqual(0, await db.ExternalAccounts.CountAsync());
            Assert.AreEqual(1, await db.ExternalPlaylists.CountAsync());
            Assert.AreEqual(1, await db.Playlists.CountAsync());
            Assert.AreEqual(2, await db.PlaylistItems.CountAsync());
            var firstItem = await db.PlaylistItems.SingleAsync(item => item.ProviderItemId == "https://artist.bandcamp.com/track/first-bandcamp-track");
            StringAssert.Contains(firstItem.SnapshotJson, "\"ExternalUrl\":\"https://artist.bandcamp.com/track/first-bandcamp-track\"");
            StringAssert.Contains(firstItem.SnapshotJson, "\"ArtworkUrl\":\"https://f4.bcbits.com/img/a1.jpg\"");
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task BandcampImportedPlaylist_DownloadMissingItemsBecomeLocalAvailable()
    {
        var bandcamp = new QueueBandcampHandler(
        [
            HtmlResponse("""
            <html>
              <head>
                <script type="application/ld+json">
                {
                  "@context": "https://schema.org",
                  "@type": "MusicAlbum",
                  "name": "Bandcamp Fixture",
                  "url": "https://artist.bandcamp.com/album/bandcamp-fixture",
                  "image": "https://f4.bcbits.com/img/a1.jpg",
                  "byArtist": { "@type": "MusicGroup", "name": "Bandcamp Artist" },
                  "track": {
                    "@type": "ItemList",
                    "itemListElement": [
                      {
                        "@type": "ListItem",
                        "item": {
                          "@type": "MusicRecording",
                          "name": "First Bandcamp Track",
                          "url": "https://artist.bandcamp.com/track/first-bandcamp-track",
                          "duration": "PT2M"
                        }
                      },
                      {
                        "@type": "ListItem",
                        "item": {
                          "@type": "MusicRecording",
                          "name": "Second Bandcamp Track",
                          "url": "https://artist.bandcamp.com/track/second-bandcamp-track",
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
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot, bandcampHandler: bandcamp);
        SeedMockSoulseekFile(tempRoot, "Bandcamp Artist", "Bandcamp Fixture", "01. Bandcamp Artist - First Bandcamp Track.mp3");
        SeedMockSoulseekFile(tempRoot, "Bandcamp Artist", "Bandcamp Fixture", "02. Bandcamp Artist - Second Bandcamp Track.mp3");
        await app.StartAsync();
        try
        {
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var imported = await client.ImportProviderPublicUrlAsync(
                "bandcamp",
                new ImportProviderPublicUrlRequestDto(
                    "https://artist.bandcamp.com/album/bandcamp-fixture",
                    "Copy"));
            var submitted = await client.DownloadMissingPlaylistItemsAsync(imported.PlaylistId);
            var synced = await WaitForPlaylistSummaryAsync(
                client,
                imported.PlaylistId,
                detail => detail.Resolution.AvailableLocalItems == 2
                    && detail.Resolution.DownloadingItems == 0,
                timeoutMs: 10000);

            Assert.IsNotNull(submitted);
            Assert.AreEqual(2, submitted.SubmittedItems);
            Assert.AreEqual(0, submitted.FailedItems);
            Assert.AreEqual("bandcamp", imported.ProviderId);
            Assert.AreEqual("Bandcamp Fixture", synced.Name);
            Assert.AreEqual(2, synced.Resolution.AvailableLocalItems);
            Assert.IsTrue(synced.Items.All(item => item.Status == "AvailableLocal"));
            Assert.IsTrue(synced.Items.All(item => item.CanonicalTrackId.HasValue));
            Assert.AreEqual(1, bandcamp.Requests.Count);
            CollectionAssert.DoesNotContain(bandcamp.Requests[0].Headers.ToArray(), "Cookie");
            CollectionAssert.DoesNotContain(bandcamp.Requests[0].Headers.ToArray(), "Authorization");

            await using var verifyScope = app.Services.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            Assert.AreEqual(0, await db.ExternalAccounts.CountAsync());
            Assert.AreEqual(2, await db.DownloadWorkflows.CountAsync(workflow => workflow.PlaylistItemId.HasValue));
            Assert.AreEqual(2, await db.LocalMediaFiles.CountAsync(file => file.Availability == (int)LocalMediaAvailability.Available));
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    private static async Task<Guid> SeedExternalAccountAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ServerDatabaseMigrationService>().EnsureMigratedAsync();
        var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
        var secretReference = "secret://windows-dpapi/" + Guid.NewGuid().ToString("N");

        var account = new ExternalAccountEntity
        {
            Id = Guid.NewGuid(),
            Provider = (int)ExternalProvider.Spotify,
            ExternalUserId = "user-1",
            DisplayName = "Alice",
            SecretReference = secretReference,
            Status = (int)ExternalAccountStatus.Authorized,
            LastAuthorizedAtUtc = new DateTimeOffset(2026, 9, 23, 8, 0, 0, TimeSpan.Zero),
        };
        var playlist = new ExternalPlaylistEntity
        {
            Id = Guid.NewGuid(),
            Account = account,
            Provider = (int)ExternalProvider.Spotify,
            ExternalId = "playlist-1",
            Name = "Daily Mix",
            SnapshotVersion = 1,
            LastSyncedAtUtc = new DateTimeOffset(2026, 9, 23, 8, 5, 0, TimeSpan.Zero),
        };

        db.AddRange(account, playlist);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static WebApplication CreateApp(
        out string url,
        out string sessionToken,
        out string tempRoot,
        QueueBandcampHandler? bandcampHandler = null,
        QueueSpotifyHandler? spotifyHandler = null,
        QueueYouTubeHandler? youtubeHandler = null)
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "Sockseek-provider-test-" + Guid.NewGuid());
        var musicRoot = Path.Combine(tempRoot, "music");
        var outputRoot = Path.Combine(tempRoot, "downloads");
        Directory.CreateDirectory(musicRoot);
        Directory.CreateDirectory(outputRoot);
        url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        sessionToken = "provider-test-token";
        return ServerHost.Build([], new ServerOptions
        {
            DatabasePath = Path.Combine(tempRoot, "sockseek.db"),
            Engine = new EngineSettings
            {
                MockFilesDir = musicRoot,
                MockFilesReadTags = false,
            },
            DefaultDownload = new DownloadSettings
            {
                Output =
                {
                    ParentDir = outputRoot,
                },
            },
            Profiles = ProfileCatalog.Empty,
            SecretStoreFactory = () => new InMemorySecretStore(),
            SessionToken = sessionToken,
            Bandcamp = new BandcampServerOptions
            {
                HttpMessageHandlerFactory = bandcampHandler == null ? null : () => bandcampHandler,
            },
            Spotify = new SpotifyServerOptions
            {
                ClientId = spotifyHandler == null ? null : "spotify-client",
                AccountsBaseUri = "https://accounts.spotify.test/",
                ApiBaseUri = "https://api.spotify.test/v1/",
                HttpMessageHandlerFactory = spotifyHandler == null ? null : () => spotifyHandler,
            },
            YouTube = new YouTubeServerOptions
            {
                ClientId = youtubeHandler == null ? null : "youtube-client",
                AuthorizationEndpointUri = "https://accounts.google.test/o/oauth2/v2/auth",
                TokenEndpointUri = "https://oauth2.googleapis.test/token",
                ApiBaseUri = "https://youtube.googleapis.test/youtube/v3/",
                HttpMessageHandlerFactory = youtubeHandler == null ? null : () => youtubeHandler,
            },
        }, url);
    }

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static HttpResponseMessage HtmlResponse(string html)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html"),
        };

    private static void SeedMockSoulseekFile(string tempRoot, string artist, string album, string filename)
    {
        var directory = Path.Combine(tempRoot, "music", artist, album);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, filename), new string('a', 4096));
    }

    private static async Task<PlaylistDetailDto> WaitForPlaylistSummaryAsync(
        SockseekApiClient client,
        Guid playlistId,
        Func<PlaylistDetailDto, bool> predicate,
        int timeoutMs = 5000)
    {
        using var timeout = new CancellationTokenSource(timeoutMs);
        PlaylistDetailDto? last = null;

        while (!timeout.IsCancellationRequested)
        {
            last = await client.GetPlaylistAsync(playlistId, timeout.Token);
            if (last != null && predicate(last))
                return last;

            try
            {
                await Task.Delay(100, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Assert.Fail($"Timed out waiting for playlist {playlistId} to reach expected summary. Last detail: {last}.");
        return null!;
    }

    private sealed class QueueBandcampHandler(IReadOnlyList<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private int index;

        public List<RecordedBandcampRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedBandcampRequest(
                request.RequestUri ?? new Uri("about:blank"),
                request.Method,
                request.Headers.Select(header => header.Key).ToArray()));

            if (index >= responses.Count)
                throw new InvalidOperationException("No queued Bandcamp fixture response is available.");

            return Task.FromResult(responses[index++]);
        }
    }

    private sealed record RecordedBandcampRequest(
        Uri Uri,
        HttpMethod Method,
        IReadOnlyList<string> Headers);

    private sealed class QueueSpotifyHandler(IReadOnlyList<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private int index;

        public List<RecordedSpotifyRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedSpotifyRequest(
                request.RequestUri ?? new Uri("about:blank"),
                request.Method,
                request.Headers.Authorization?.Parameter,
                request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            if (index >= responses.Count)
                throw new InvalidOperationException("No queued Spotify fixture response is available.");

            return responses[index++];
        }
    }

    private sealed record RecordedSpotifyRequest(
        Uri Uri,
        HttpMethod Method,
        string? AuthorizationParameter,
        string? Body);

    private sealed class QueueYouTubeHandler(IReadOnlyList<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private int index;

        public List<RecordedYouTubeRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedYouTubeRequest(
                request.RequestUri ?? new Uri("about:blank"),
                request.Method,
                request.Headers.Authorization?.Parameter,
                request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            if (index >= responses.Count)
                throw new InvalidOperationException("No queued YouTube fixture response is available.");

            return responses[index++];
        }
    }

    private sealed record RecordedYouTubeRequest(
        Uri Uri,
        HttpMethod Method,
        string? AuthorizationParameter,
        string? Body);

    private static void DeleteTempRoot(string tempRoot)
    {
        if (!Directory.Exists(tempRoot))
            return;

        SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 19)
            {
                Thread.Sleep(250);
                SqliteConnection.ClearAllPools();
            }
            catch (UnauthorizedAccessException) when (attempt < 19)
            {
                Thread.Sleep(250);
                SqliteConnection.ClearAllPools();
            }
        }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
