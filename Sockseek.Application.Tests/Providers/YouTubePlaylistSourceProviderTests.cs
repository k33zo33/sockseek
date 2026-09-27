using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Providers;
using Sockseek.Application.Security;
using Sockseek.Integrations.Abstractions;
using Sockseek.Integrations.YouTube;

namespace Tests.Application.Providers;

[TestClass]
public sealed class YouTubePlaylistSourceProviderTests
{
    [TestMethod]
    public async Task StartAuthorization_UsesReadonlyYouTubeScopeAndPkce()
    {
        var provider = CreateProvider(new QueueHttpMessageHandler([]), new RecordingSecretStore());

        var start = await provider.StartAuthorizationAsync(new AuthorizationRequest(
            ProviderIds.YouTube,
            new Uri("http://127.0.0.1:49152/callback"),
            ["streaming", "download"],
            "state-1",
            "challenge-1",
            OAuthPkceCoordinator.CodeChallengeMethod), CancellationToken.None);

        var query = ParseQuery(start.AuthorizationUri);
        Assert.AreEqual("youtube-client", query["client_id"]);
        Assert.AreEqual("code", query["response_type"]);
        Assert.AreEqual("http://127.0.0.1:49152/callback", query["redirect_uri"]);
        Assert.AreEqual("https://www.googleapis.com/auth/youtube.readonly", query["scope"]);
        Assert.AreEqual("state-1", query["state"]);
        Assert.AreEqual("challenge-1", query["code_challenge"]);
        Assert.AreEqual(OAuthPkceCoordinator.CodeChallengeMethod, query["code_challenge_method"]);
        Assert.AreEqual("offline", query["access_type"]);
        Assert.AreEqual("consent", query["prompt"]);
        Assert.IsFalse(query["scope"].Contains("streaming", StringComparison.Ordinal));
        Assert.IsFalse(query["scope"].Contains("download", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CompleteAuthorization_ExchangesPkceCodeAndStoresTokensInSecretStore()
    {
        var handler = new QueueHttpMessageHandler(
        [
            TokenResponse("access-1", "refresh-1"),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "channel-1",
                  "snippet": { "title": "Channel One" }
                }
              ]
            }
            """),
        ]);
        var secretStore = new RecordingSecretStore();
        var provider = CreateProvider(handler, secretStore);

        var account = await provider.CompleteAuthorizationAsync(new AuthorizationCallback(
            ProviderIds.YouTube,
            new Uri("http://127.0.0.1:49152/callback"),
            "state-1",
            "auth-code-1",
            null,
            "verifier-1"), CancellationToken.None);

        Assert.AreEqual(ProviderIds.YouTube, account.ProviderId);
        Assert.AreEqual("channel-1", account.ExternalUserId);
        Assert.AreEqual("Channel One", account.DisplayName);
        Assert.AreEqual("secret://youtube/1", account.SecretReference);
        StringAssert.Contains(handler.Requests[0].Body!, "grant_type=authorization_code");
        StringAssert.Contains(handler.Requests[0].Body!, "code=auth-code-1");
        StringAssert.Contains(handler.Requests[0].Body!, "code_verifier=verifier-1");
        StringAssert.Contains(handler.Requests[0].Body!, "client_id=youtube-client");
        Assert.AreEqual("access-1", secretStore.Saved[account.SecretReference]["access_token"]);
        Assert.AreEqual("refresh-1", secretStore.Saved[account.SecretReference]["refresh_token"]);
        Assert.AreEqual("Bearer", handler.Requests[1].AuthorizationScheme);
        Assert.AreEqual("access-1", handler.Requests[1].AuthorizationParameter);
    }

    [TestMethod]
    public async Task GetPlaylistsAsync_FollowsYouTubePagination()
    {
        var handler = new QueueHttpMessageHandler(
        [
            TokenResponse("access-1", "refresh-1"),
            ChannelResponse(),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "playlist-1",
                  "snippet": { "title": "First Mix", "publishedAt": "2026-01-01T00:00:00Z" },
                  "contentDetails": { "itemCount": 11 }
                }
              ],
              "nextPageToken": "page-2"
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "playlist-2",
                  "snippet": { "title": "Second Mix" },
                  "contentDetails": { "itemCount": 7 }
                }
              ]
            }
            """),
        ]);
        var provider = CreateProvider(handler, new RecordingSecretStore());
        var account = await ConnectAsync(provider);

        var playlists = await provider.GetPlaylistsAsync(account.AccountId, CancellationToken.None);

        Assert.AreEqual(2, playlists.Count);
        Assert.AreEqual("playlist-1", playlists[0].ExternalPlaylistId);
        Assert.AreEqual("First Mix", playlists[0].Name);
        Assert.AreEqual("https://www.youtube.com/playlist?list=playlist-1", playlists[0].Url);
        Assert.AreEqual(11, playlists[0].ItemCount);
        Assert.AreEqual("playlist-2", playlists[1].ExternalPlaylistId);
        Assert.AreEqual("/youtube/v3/playlists", handler.Requests[2].Uri.AbsolutePath);
        Assert.AreEqual("pageToken=page-2", handler.Requests[3].Uri.Query.TrimStart('?').Split('&').Single(part => part.StartsWith("pageToken=", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task GetPlaylistAsync_MapsPublicPrivateDeletedItemsAndDurations()
    {
        var handler = new QueueHttpMessageHandler(
        [
            TokenResponse("access-1", "refresh-1"),
            ChannelResponse(),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "playlist-1",
                  "snippet": { "title": "Fixture Mix", "publishedAt": "2026-01-01T00:00:00Z" },
                  "contentDetails": { "itemCount": 3 }
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
                    "title": "Public Fixture Video",
                    "position": 0,
                    "channelTitle": "Playlist Owner",
                    "videoOwnerChannelTitle": "Video Channel",
                    "resourceId": { "kind": "youtube#video", "videoId": "video-1" },
                    "thumbnails": {
                      "default": { "url": "https://img.youtube.test/default.jpg" },
                      "high": { "url": "https://img.youtube.test/high.jpg" }
                    }
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
              ],
              "nextPageToken": "page-2"
            }
            """),
            JsonResponse("""
            {
              "items": [
                {
                  "id": "playlist-item-3",
                  "snippet": {
                    "title": "Deleted video",
                    "position": 2,
                    "resourceId": { "kind": "youtube#video", "videoId": "deleted-video" }
                  },
                  "contentDetails": { "videoId": "deleted-video" },
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
                  "snippet": { "title": "Public Video From Lookup", "channelTitle": "Video Channel" },
                  "contentDetails": { "duration": "PT3M3S" },
                  "status": { "privacyStatus": "public" }
                }
              ]
            }
            """),
        ]);
        var provider = CreateProvider(handler, new RecordingSecretStore());
        var account = await ConnectAsync(provider);

        var snapshot = await provider.GetPlaylistAsync(new ExternalPlaylistRequest(
            account.AccountId,
            ProviderIds.YouTube,
            "playlist-1",
            null), CancellationToken.None);

        Assert.AreEqual("Fixture Mix", snapshot.Name);
        Assert.AreEqual("https://www.youtube.com/playlist?list=playlist-1", snapshot.Url);
        Assert.AreEqual(3, snapshot.Items.Count);
        Assert.AreEqual("video-1", snapshot.Items[0].ExternalTrackId);
        Assert.AreEqual("playlist-item-1", snapshot.Items[0].ProviderItemId);
        Assert.AreEqual("Public Video From Lookup", snapshot.Items[0].Title);
        CollectionAssert.AreEqual(new[] { "Video Channel" }, snapshot.Items[0].Artists.ToArray());
        Assert.AreEqual(183000, snapshot.Items[0].DurationMs);
        Assert.AreEqual("https://www.youtube.com/watch?v=video-1", snapshot.Items[0].ExternalUrl);
        Assert.AreEqual("https://img.youtube.test/high.jpg", snapshot.Items[0].ArtworkUrl);
        Assert.AreEqual("private-video", snapshot.Items[1].ExternalTrackId);
        Assert.AreEqual("Private video", snapshot.Items[1].Title);
        Assert.AreEqual("https://www.youtube.com/watch?v=private-video", snapshot.Items[1].ExternalUrl);
        Assert.AreEqual("deleted-video", snapshot.Items[2].ExternalTrackId);
        Assert.AreEqual("Deleted video", snapshot.Items[2].Title);
        StringAssert.Contains(snapshot.Items[0].RawMetadataJson, "playlistItem");
        StringAssert.Contains(snapshot.Items[0].RawMetadataJson, "video");
    }

    [TestMethod]
    public async Task GetPlaylistsAsync_RefreshesAccessTokenAfterUnauthorizedResponse()
    {
        var handler = new QueueHttpMessageHandler(
        [
            TokenResponse("expired-access", "refresh-1"),
            ChannelResponse(),
            JsonResponse("""{ "error": { "code": 401, "message": "Invalid Credentials" } }""", HttpStatusCode.Unauthorized),
            TokenResponse("fresh-access", null),
            JsonResponse("""{ "items": [] }"""),
        ]);
        var secretStore = new RecordingSecretStore();
        var provider = CreateProvider(handler, secretStore);
        var account = await ConnectAsync(provider);

        var playlists = await provider.GetPlaylistsAsync(account.AccountId, CancellationToken.None);

        Assert.AreEqual(0, playlists.Count);
        Assert.AreEqual("expired-access", handler.Requests[2].AuthorizationParameter);
        Assert.AreEqual("fresh-access", handler.Requests[4].AuthorizationParameter);
        Assert.IsTrue(secretStore.DeletedReferences.Contains("secret://youtube/1"));
        Assert.AreEqual("fresh-access", secretStore.Saved["secret://youtube/2"]["access_token"]);
        Assert.AreEqual("refresh-1", secretStore.Saved["secret://youtube/2"]["refresh_token"]);
    }

    [TestMethod]
    public async Task GetPlaylistsAsync_RefreshFailureRequiresReauthorization()
    {
        var handler = new QueueHttpMessageHandler(
        [
            TokenResponse("expired-access", "refresh-1"),
            ChannelResponse(),
            JsonResponse("""{ "error": { "code": 401, "message": "Invalid Credentials" } }""", HttpStatusCode.Unauthorized),
            JsonResponse("""{ "error": "invalid_grant", "error_description": "Token has been expired or revoked." }""", HttpStatusCode.BadRequest),
        ]);
        var provider = CreateProvider(handler, new RecordingSecretStore());
        var account = await ConnectAsync(provider);

        var exception = await Assert.ThrowsExceptionAsync<YouTubeProviderException>(
            () => provider.GetPlaylistsAsync(account.AccountId, CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.IsTrue(exception.ReauthorizationRequired);
        StringAssert.Contains(exception.Message, "Reconnect YouTube");
    }

    [TestMethod]
    public async Task GetPlaylistsAsync_RateLimitExposesRetryAfter()
    {
        var rateLimit = JsonResponse("""{ "error": { "code": 429, "message": "Quota exceeded" } }""", HttpStatusCode.TooManyRequests);
        rateLimit.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(9));
        var handler = new QueueHttpMessageHandler(
        [
            TokenResponse("access-1", "refresh-1"),
            ChannelResponse(),
            rateLimit,
        ]);
        var provider = CreateProvider(handler, new RecordingSecretStore());
        var account = await ConnectAsync(provider);

        var exception = await Assert.ThrowsExceptionAsync<YouTubeProviderException>(
            () => provider.GetPlaylistsAsync(account.AccountId, CancellationToken.None));

        Assert.AreEqual(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.AreEqual(TimeSpan.FromSeconds(9), exception.RetryAfter);
    }

    [TestMethod]
    public void YouTubeProvider_DoesNotExposePlaybackOrDownloadMethods()
    {
        var publicMembers = typeof(YouTubePlaylistSourceProvider)
            .GetMethods()
            .Select(method => method.Name)
            .Concat(typeof(IPlaylistSourceProvider).GetMethods().Select(method => method.Name))
            .ToArray();

        CollectionAssert.DoesNotContain(publicMembers, "GetAudioStreamAsync");
        CollectionAssert.DoesNotContain(publicMembers, "DownloadTrackAsync");
        CollectionAssert.DoesNotContain(publicMembers, "PlayAsync");
        CollectionAssert.DoesNotContain(publicMembers, "GetPlaybackUrlAsync");
        CollectionAssert.DoesNotContain(publicMembers, "GetMediaUrlAsync");
    }

    private static YouTubePlaylistSourceProvider CreateProvider(
        QueueHttpMessageHandler handler,
        RecordingSecretStore secretStore)
        => new(
            new HttpClient(handler),
            secretStore,
            new YouTubePlaylistSourceOptions
            {
                ClientId = "youtube-client",
                AuthorizationEndpointUri = new Uri("https://accounts.google.test/o/oauth2/v2/auth"),
                TokenEndpointUri = new Uri("https://oauth2.googleapis.test/token"),
                ApiBaseUri = new Uri("https://youtube.googleapis.test/youtube/v3/"),
            });

    private static async Task<ExternalAccountSnapshot> ConnectAsync(YouTubePlaylistSourceProvider provider)
        => await provider.CompleteAuthorizationAsync(new AuthorizationCallback(
            ProviderIds.YouTube,
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
          "scope": "https://www.googleapis.com/auth/youtube.readonly"
          {{refreshJson}}
        }
        """);
    }

    private static HttpResponseMessage ChannelResponse()
        => JsonResponse("""
        {
          "items": [
            {
              "id": "channel-1",
              "snippet": { "title": "Channel One" }
            }
          ]
        }
        """);

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
                throw new InvalidOperationException("No queued YouTube fixture response is available.");

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
            var reference = "secret://youtube/" + nextId++;
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
