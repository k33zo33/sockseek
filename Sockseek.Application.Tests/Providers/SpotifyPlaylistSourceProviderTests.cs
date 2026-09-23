using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Providers;
using Sockseek.Application.Security;
using Sockseek.Integrations.Abstractions;
using Sockseek.Integrations.Spotify;

namespace Tests.Application.Providers;

[TestClass]
public sealed class SpotifyPlaylistSourceProviderTests
{
    [TestMethod]
    public async Task StartAuthorization_UsesMinimalReadOnlyPlaylistScopes()
    {
        var provider = CreateProvider(new QueueHttpMessageHandler([]), new RecordingSecretStore());

        var start = await provider.StartAuthorizationAsync(new AuthorizationRequest(
            ProviderIds.Spotify,
            new Uri("http://127.0.0.1:49152/callback"),
            ["streaming", "user-library-read"],
            "state-1",
            "challenge-1",
            OAuthPkceCoordinator.CodeChallengeMethod), CancellationToken.None);

        var query = ParseQuery(start.AuthorizationUri);
        Assert.AreEqual("spotify-client", query["client_id"]);
        Assert.AreEqual("code", query["response_type"]);
        Assert.AreEqual("http://127.0.0.1:49152/callback", query["redirect_uri"]);
        Assert.AreEqual("playlist-read-private playlist-read-collaborative", query["scope"]);
        Assert.AreEqual("state-1", query["state"]);
        Assert.AreEqual("challenge-1", query["code_challenge"]);
        Assert.AreEqual(OAuthPkceCoordinator.CodeChallengeMethod, query["code_challenge_method"]);
        Assert.IsFalse(query["scope"].Contains("streaming", StringComparison.Ordinal));
        Assert.IsFalse(query["scope"].Contains("user-library-read", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CompleteAuthorization_ExchangesPkceCodeAndStoresTokensInSecretStore()
    {
        var handler = new QueueHttpMessageHandler(
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
            JsonResponse("""
            {
              "id": "spotify-user-1",
              "display_name": "Spotify User"
            }
            """),
        ]);
        var secretStore = new RecordingSecretStore();
        var provider = CreateProvider(handler, secretStore);

        var account = await provider.CompleteAuthorizationAsync(new AuthorizationCallback(
            ProviderIds.Spotify,
            new Uri("http://127.0.0.1:49152/callback"),
            "state-1",
            "auth-code-1",
            null,
            "verifier-1"), CancellationToken.None);

        Assert.AreEqual(ProviderIds.Spotify, account.ProviderId);
        Assert.AreEqual("spotify-user-1", account.ExternalUserId);
        Assert.AreEqual("Spotify User", account.DisplayName);
        Assert.AreEqual("secret://spotify/1", account.SecretReference);
        StringAssert.Contains(handler.Requests[0].Body!, "grant_type=authorization_code");
        StringAssert.Contains(handler.Requests[0].Body!, "code=auth-code-1");
        StringAssert.Contains(handler.Requests[0].Body!, "code_verifier=verifier-1");
        StringAssert.Contains(handler.Requests[0].Body!, "client_id=spotify-client");
        Assert.AreEqual("access-1", secretStore.Saved[account.SecretReference]["access_token"]);
        Assert.AreEqual("refresh-1", secretStore.Saved[account.SecretReference]["refresh_token"]);
        Assert.IsTrue(secretStore.ExpiresAtUtc[account.SecretReference] > DateTimeOffset.UtcNow);
    }

    [TestMethod]
    public async Task GetPlaylistsAsync_FollowsSpotifyPagination()
    {
        var handler = new QueueHttpMessageHandler(
        [
            TokenResponse("access-1", "refresh-1"),
            UserProfileResponse(),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "playlist-1",
                  "name": "First Mix",
                  "external_urls": { "spotify": "https://open.spotify.com/playlist/playlist-1" },
                  "tracks": { "total": 11 }
                }
              ],
              "next": "https://api.spotify.test/v1/me/playlists?offset=50&limit=50"
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "playlist-2",
                  "name": "Second Mix",
                  "external_urls": { "spotify": "https://open.spotify.com/playlist/playlist-2" },
                  "tracks": { "total": 7 }
                }
              ],
              "next": null
            }
            """),
        ]);
        var provider = CreateProvider(handler, new RecordingSecretStore());
        var account = await ConnectAsync(provider);

        var playlists = await provider.GetPlaylistsAsync(account.AccountId, CancellationToken.None);

        Assert.AreEqual(2, playlists.Count);
        Assert.AreEqual("playlist-1", playlists[0].ExternalPlaylistId);
        Assert.AreEqual(11, playlists[0].ItemCount);
        Assert.AreEqual("https://open.spotify.com/playlist/playlist-2", playlists[1].Url);
        Assert.AreEqual("Bearer", handler.Requests[2].AuthorizationScheme);
        Assert.AreEqual("access-1", handler.Requests[2].AuthorizationParameter);
        Assert.AreEqual("/v1/me/playlists", handler.Requests[2].Uri.AbsolutePath);
        Assert.AreEqual("/v1/me/playlists", handler.Requests[3].Uri.AbsolutePath);
    }

    [TestMethod]
    public async Task GetPlaylistAsync_MapsTracksEpisodesUnavailableAndLocalItems()
    {
        var handler = new QueueHttpMessageHandler(
        [
            TokenResponse("access-1", "refresh-1"),
            UserProfileResponse(),
            JsonResponse("""
            {
              "id": "playlist-1",
              "name": "Fixture Mix",
              "snapshot_id": "snapshot-1",
              "external_urls": { "spotify": "https://open.spotify.com/playlist/playlist-1" }
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "added_at": "2026-01-01T00:00:00Z",
                  "track": {
                    "type": "track",
                    "id": "track-1",
                    "uri": "spotify:track:track-1",
                    "name": "Normal Track",
                    "duration_ms": 181000,
                    "external_urls": { "spotify": "https://open.spotify.com/track/track-1" },
                    "external_ids": { "isrc": "USRC17607839" },
                    "artists": [{ "name": "First Artist" }],
                    "album": {
                      "name": "First Album",
                      "images": [
                        { "url": "https://images.example/64.jpg", "width": 64, "height": 64 },
                        { "url": "https://images.example/640.jpg", "width": 640, "height": 640 }
                      ]
                    }
                  }
                },
                {
                  "track": {
                    "type": "track",
                    "id": null,
                    "uri": "spotify:track:unavailable",
                    "name": "Unavailable Track",
                    "duration_ms": 120000,
                    "artists": [{ "name": "Missing Artist" }],
                    "album": { "name": "Missing Album", "images": [] }
                  }
                },
                {
                  "track": {
                    "type": "episode",
                    "id": "episode-1",
                    "uri": "spotify:episode:episode-1",
                    "name": "Podcast Episode",
                    "duration_ms": 2400000,
                    "external_urls": { "spotify": "https://open.spotify.com/episode/episode-1" },
                    "show": {
                      "name": "Podcast Show",
                      "publisher": "Podcast Network",
                      "images": [{ "url": "https://images.example/show.jpg", "width": 300, "height": 300 }]
                    }
                  }
                },
                {
                  "track": {
                    "type": "track",
                    "is_local": true,
                    "id": null,
                    "uri": "spotify:local:Artist:Album:Local%20Track:200",
                    "name": "Local Track",
                    "duration_ms": 200000,
                    "artists": [{ "name": "Local Artist" }],
                    "album": { "name": "Local Album", "images": [] }
                  }
                }
              ],
              "next": null
            }
            """),
        ]);
        var provider = CreateProvider(handler, new RecordingSecretStore());
        var account = await ConnectAsync(provider);

        var snapshot = await provider.GetPlaylistAsync(new ExternalPlaylistRequest(
            account.AccountId,
            ProviderIds.Spotify,
            "playlist-1",
            null), CancellationToken.None);

        Assert.AreEqual(4, snapshot.Items.Count);
        Assert.AreEqual("Fixture Mix", snapshot.Name);
        Assert.AreEqual("track-1", snapshot.Items[0].ExternalTrackId);
        Assert.AreEqual("spotify:track:track-1", snapshot.Items[0].ProviderItemId);
        Assert.AreEqual("Normal Track", snapshot.Items[0].Title);
        CollectionAssert.AreEqual(new[] { "First Artist" }, snapshot.Items[0].Artists.ToArray());
        Assert.AreEqual("First Album", snapshot.Items[0].Album);
        Assert.AreEqual(181000, snapshot.Items[0].DurationMs);
        Assert.AreEqual("USRC17607839", snapshot.Items[0].Isrc);
        Assert.AreEqual("https://open.spotify.com/track/track-1", snapshot.Items[0].ExternalUrl);
        Assert.AreEqual("https://images.example/640.jpg", snapshot.Items[0].ArtworkUrl);
        Assert.AreEqual("spotify:track:unavailable", snapshot.Items[1].ExternalTrackId);
        Assert.AreEqual("Unavailable Track", snapshot.Items[1].Title);
        Assert.AreEqual("episode-1", snapshot.Items[2].ExternalTrackId);
        CollectionAssert.AreEqual(new[] { "Podcast Network" }, snapshot.Items[2].Artists.ToArray());
        Assert.AreEqual("Podcast Show", snapshot.Items[2].Album);
        Assert.AreEqual("https://images.example/show.jpg", snapshot.Items[2].ArtworkUrl);
        Assert.AreEqual("spotify:local:Artist:Album:Local%20Track:200", snapshot.Items[3].ExternalTrackId);
        Assert.AreEqual("Local Track", snapshot.Items[3].Title);
        Assert.AreEqual(3, snapshot.Items[3].Position);
    }

    [TestMethod]
    public async Task GetPlaylistsAsync_RefreshesAccessTokenAfterUnauthorizedResponse()
    {
        var handler = new QueueHttpMessageHandler(
        [
            TokenResponse("expired-access", "refresh-1"),
            UserProfileResponse(),
            JsonResponse("""{ "error": { "status": 401, "message": "The access token expired" } }""", HttpStatusCode.Unauthorized),
            TokenResponse("fresh-access", null),
            JsonResponse("""{ "items": [], "next": null }"""),
        ]);
        var secretStore = new RecordingSecretStore();
        var provider = CreateProvider(handler, secretStore);
        var account = await ConnectAsync(provider);

        var playlists = await provider.GetPlaylistsAsync(account.AccountId, CancellationToken.None);

        Assert.AreEqual(0, playlists.Count);
        Assert.AreEqual("expired-access", handler.Requests[2].AuthorizationParameter);
        Assert.AreEqual("fresh-access", handler.Requests[4].AuthorizationParameter);
        Assert.IsTrue(secretStore.DeletedReferences.Contains("secret://spotify/1"));
        Assert.AreEqual("fresh-access", secretStore.Saved["secret://spotify/2"]["access_token"]);
        Assert.AreEqual("refresh-1", secretStore.Saved["secret://spotify/2"]["refresh_token"]);
    }

    [TestMethod]
    public async Task GetPlaylistsAsync_ForbiddenUsesDevelopmentModeMessage()
    {
        var handler = new QueueHttpMessageHandler(
        [
            TokenResponse("access-1", "refresh-1"),
            UserProfileResponse(),
            JsonResponse("""{ "error": { "status": 403, "message": "User not registered in the Developer Dashboard" } }""", HttpStatusCode.Forbidden),
        ]);
        var provider = CreateProvider(handler, new RecordingSecretStore());
        var account = await ConnectAsync(provider);

        var exception = await Assert.ThrowsExceptionAsync<SpotifyProviderException>(
            () => provider.GetPlaylistsAsync(account.AccountId, CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.Forbidden, exception.StatusCode);
        StringAssert.Contains(exception.Message, "development-mode");
        StringAssert.Contains(exception.Message, "allowlist");
    }

    [TestMethod]
    public async Task GetPlaylistsAsync_RateLimitExposesRetryAfter()
    {
        var rateLimit = JsonResponse("""{ "error": { "status": 429, "message": "API rate limit exceeded" } }""", HttpStatusCode.TooManyRequests);
        rateLimit.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(9));
        var handler = new QueueHttpMessageHandler(
        [
            TokenResponse("access-1", "refresh-1"),
            UserProfileResponse(),
            rateLimit,
        ]);
        var provider = CreateProvider(handler, new RecordingSecretStore());
        var account = await ConnectAsync(provider);

        var exception = await Assert.ThrowsExceptionAsync<SpotifyProviderException>(
            () => provider.GetPlaylistsAsync(account.AccountId, CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.AreEqual(TimeSpan.FromSeconds(9), exception.RetryAfter);
    }

    [TestMethod]
    public void SpotifyProvider_DoesNotExposePlaybackOrDownloadMethods()
    {
        var publicMembers = typeof(SpotifyPlaylistSourceProvider)
            .GetMethods()
            .Select(method => method.Name)
            .Concat(typeof(IPlaylistSourceProvider).GetMethods().Select(method => method.Name))
            .ToArray();

        CollectionAssert.DoesNotContain(publicMembers, "GetAudioStreamAsync");
        CollectionAssert.DoesNotContain(publicMembers, "DownloadTrackAsync");
        CollectionAssert.DoesNotContain(publicMembers, "PlayAsync");
        CollectionAssert.DoesNotContain(publicMembers, "GetPlaybackUrlAsync");
    }

    private static SpotifyPlaylistSourceProvider CreateProvider(
        QueueHttpMessageHandler handler,
        RecordingSecretStore secretStore)
        => new(
            new HttpClient(handler),
            secretStore,
            new SpotifyPlaylistSourceOptions
            {
                ClientId = "spotify-client",
                AccountsBaseUri = new Uri("https://accounts.spotify.test/"),
                ApiBaseUri = new Uri("https://api.spotify.test/v1/"),
            });

    private static async Task<ExternalAccountSnapshot> ConnectAsync(SpotifyPlaylistSourceProvider provider)
        => await provider.CompleteAuthorizationAsync(new AuthorizationCallback(
            ProviderIds.Spotify,
            new Uri("http://127.0.0.1:49152/callback"),
            "state-1",
            "auth-code-1",
            null,
            "verifier-1"), CancellationToken.None);

    private static HttpResponseMessage TokenResponse(string accessToken, string? refreshToken)
    {
        var refreshJson = refreshToken == null ? string.Empty : $""" , "refresh_token": "{refreshToken}" """;
        return JsonResponse($$"""
        {
          "access_token": "{{accessToken}}",
          "token_type": "Bearer",
          "expires_in": 3600,
          "scope": "playlist-read-private playlist-read-collaborative"
          {{refreshJson}}
        }
        """);
    }

    private static HttpResponseMessage UserProfileResponse()
        => JsonResponse("""{ "id": "spotify-user-1", "display_name": "Spotify User" }""");

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static Dictionary<string, string> ParseQuery(Uri uri)
    {
        var query = uri.Query.TrimStart('?');
        return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty,
                StringComparer.Ordinal);
    }

    private sealed class QueueHttpMessageHandler(IReadOnlyList<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private int index;

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri ?? new Uri("about:blank"),
                request.Method,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            if (index >= responses.Count)
                throw new InvalidOperationException("No queued Spotify fixture response is available.");

            return responses[index++];
        }
    }

    private sealed record RecordedRequest(
        Uri Uri,
        HttpMethod Method,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? Body);

    private sealed class RecordingSecretStore : ISecretStore
    {
        private int nextId = 1;

        public Dictionary<string, Dictionary<string, string>> Saved { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, DateTimeOffset?> ExpiresAtUtc { get; } = new(StringComparer.Ordinal);

        public List<string> DeletedReferences { get; } = [];

        public Task<string> SaveAsync(SecretStoreSaveRequest request, CancellationToken cancellationToken = default)
        {
            var reference = "secret://spotify/" + nextId++;
            Saved[reference] = request.Secrets.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);
            ExpiresAtUtc[reference] = request.ExpiresAtUtc;
            return Task.FromResult(reference);
        }

        public Task<SecretStoreEntry?> ReadAsync(string secretReference, CancellationToken cancellationToken = default)
        {
            if (!Saved.TryGetValue(secretReference, out var secrets))
                return Task.FromResult<SecretStoreEntry?>(null);

            return Task.FromResult<SecretStoreEntry?>(new SecretStoreEntry(
                secretReference,
                secrets,
                ExpiresAtUtc[secretReference]));
        }

        public Task<bool> DeleteAsync(string secretReference, CancellationToken cancellationToken = default)
        {
            DeletedReferences.Add(secretReference);
            return Task.FromResult(Saved.Remove(secretReference));
        }
    }
}
