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
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot, spotify);
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
        QueueSpotifyHandler? spotifyHandler = null)
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
            Spotify = new SpotifyServerOptions
            {
                ClientId = spotifyHandler == null ? null : "spotify-client",
                AccountsBaseUri = "https://accounts.spotify.test/",
                ApiBaseUri = "https://api.spotify.test/v1/",
                HttpMessageHandlerFactory = spotifyHandler == null ? null : () => spotifyHandler,
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
