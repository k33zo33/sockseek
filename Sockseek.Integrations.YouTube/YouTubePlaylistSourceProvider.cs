using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Sockseek.Application.Security;
using Sockseek.Integrations.Abstractions;

namespace Sockseek.Integrations.YouTube;

public sealed class YouTubePlaylistSourceProvider : IPlaylistSourceProvider
{
    private const int MaxPageSize = 50;

    private readonly HttpClient httpClient;
    private readonly ISecretStore secretStore;
    private readonly YouTubePlaylistSourceOptions options;
    private readonly Dictionary<Guid, YouTubeAccountRecord> accounts = new();
    private readonly object gate = new();

    public YouTubePlaylistSourceProvider(
        HttpClient httpClient,
        ISecretStore secretStore,
        YouTubePlaylistSourceOptions options)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        this.options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
    }

    public string ProviderId => ProviderIds.YouTube;

    public PlaylistProviderCapabilities Capabilities =>
        PlaylistProviderCapabilities.ConnectAccount
        | PlaylistProviderCapabilities.ListUserPlaylists
        | PlaylistProviderCapabilities.ReadPlaylistItems
        | PlaylistProviderCapabilities.IncrementalSync
        | PlaylistProviderCapabilities.RequiresManualAppApproval;

    public void RememberAccount(ExternalAccountSnapshot account)
    {
        ArgumentNullException.ThrowIfNull(account);
        ValidateProvider(account.ProviderId, nameof(account));

        lock (gate)
        {
            accounts[account.AccountId.Value] = new YouTubeAccountRecord(
                account.AccountId,
                account.ExternalUserId,
                account.DisplayName,
                account.SecretReference,
                account.AuthorizedAtUtc);
        }
    }

    public Task<AuthorizationStartResult> StartAuthorizationAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateProvider(request.ProviderId, nameof(request));

        var query = new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = request.RedirectUri.ToString(),
            ["scope"] = string.Join(' ', options.Scopes),
            ["state"] = request.State,
            ["code_challenge"] = request.CodeChallenge,
            ["code_challenge_method"] = request.CodeChallengeMethod,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
        };

        return Task.FromResult(new AuthorizationStartResult(
            BuildUri(options.AuthorizationEndpointUri, query),
            request.State));
    }

    public async Task<ExternalAccountSnapshot> CompleteAuthorizationAsync(
        AuthorizationCallback callback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ValidateProvider(callback.ProviderId, nameof(callback));
        if (!string.IsNullOrWhiteSpace(callback.Error))
            throw new YouTubeProviderException($"YouTube authorization failed: {callback.Error}");
        ArgumentException.ThrowIfNullOrWhiteSpace(callback.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(callback.CodeVerifier);

        var tokenResponse = await PostTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = callback.Code,
                ["redirect_uri"] = callback.RedirectUri.ToString(),
                ["client_id"] = options.ClientId,
                ["code_verifier"] = callback.CodeVerifier,
            },
            cancellationToken);

        var profile = await GetCurrentChannelProfileAsync(tokenResponse.AccessToken, cancellationToken);
        var secretReference = await SaveTokenSecretAsync(tokenResponse, null, cancellationToken);
        var account = new YouTubeAccountRecord(
            new ExternalAccountId(Guid.NewGuid()),
            profile.ChannelId,
            string.IsNullOrWhiteSpace(profile.Title) ? profile.ChannelId : profile.Title,
            secretReference,
            DateTimeOffset.UtcNow);

        lock (gate)
            accounts[account.AccountId.Value] = account;

        return new ExternalAccountSnapshot(
            account.AccountId,
            ProviderId,
            account.ExternalUserId,
            account.DisplayName,
            account.SecretReference,
            account.AuthorizedAtUtc);
    }

    public async Task<IReadOnlyList<ExternalPlaylistSummary>> GetPlaylistsAsync(
        ExternalAccountId accountId,
        CancellationToken cancellationToken)
    {
        var playlists = new List<ExternalPlaylistSummary>();
        string? pageToken = null;

        do
        {
            var query = new Dictionary<string, string>
            {
                ["part"] = "snippet,contentDetails",
                ["mine"] = "true",
                ["maxResults"] = MaxPageSize.ToString(CultureInfo.InvariantCulture),
            };
            if (!string.IsNullOrWhiteSpace(pageToken))
                query["pageToken"] = pageToken;

            using var response = await SendAuthorizedAsync(
                accountId,
                accessToken => CreateBearerRequest(HttpMethod.Get, BuildApiUri("playlists", query), accessToken),
                cancellationToken);
            using var document = await ReadSuccessDocumentAsync(response, cancellationToken);
            var root = document.RootElement;
            foreach (var item in EnumerateItems(root))
                playlists.Add(MapPlaylistSummary(item));
            pageToken = GetString(root, "nextPageToken");
        }
        while (!string.IsNullOrWhiteSpace(pageToken));

        return playlists;
    }

    public async Task<ExternalPlaylistSnapshot> GetPlaylistAsync(
        ExternalPlaylistRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateProvider(request.ProviderId, nameof(request));
        if (request.AccountId is not { } accountId)
            throw new ArgumentException("YouTube playlist import requires a connected account.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExternalPlaylistId);

        var playlist = await GetPlaylistMetadataAsync(accountId, request.ExternalPlaylistId, cancellationToken);
        var pendingItems = await GetPlaylistItemsAsync(accountId, request.ExternalPlaylistId, cancellationToken);
        var videoMetadata = await GetVideoMetadataAsync(accountId, pendingItems, cancellationToken);
        var occurrenceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var items = pendingItems
            .Select((item, index) => MapPlaylistItem(request.ExternalPlaylistId, item, videoMetadata, index, occurrenceCounts))
            .ToArray();

        return new ExternalPlaylistSnapshot(
            ProviderId,
            playlist.ExternalPlaylistId,
            playlist.Name,
            playlist.Url,
            ComputeSnapshotVersion(playlist.SnapshotIdentity),
            DateTimeOffset.UtcNow,
            items);
    }

    public async Task DisconnectAsync(
        ExternalAccountId accountId,
        CancellationToken cancellationToken)
    {
        YouTubeAccountRecord? account;
        lock (gate)
            accounts.Remove(accountId.Value, out account);

        if (account != null)
            await secretStore.DeleteAsync(account.SecretReference, cancellationToken);
    }

    private async Task<YouTubePlaylistMetadata> GetPlaylistMetadataAsync(
        ExternalAccountId accountId,
        string externalPlaylistId,
        CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(
            accountId,
            accessToken => CreateBearerRequest(HttpMethod.Get, BuildApiUri("playlists", new Dictionary<string, string>
            {
                ["part"] = "snippet,contentDetails",
                ["id"] = externalPlaylistId,
                ["maxResults"] = "1",
            }), accessToken),
            cancellationToken);
        using var document = await ReadSuccessDocumentAsync(response, cancellationToken);
        var root = document.RootElement;
        var item = EnumerateItems(root).FirstOrDefault();
        if (item.ValueKind == JsonValueKind.Undefined)
            throw new KeyNotFoundException("YouTube playlist was not found.");

        var summary = MapPlaylistSummary(item);
        var publishedAt = GetString(GetObject(item, "snippet"), "publishedAt");
        return new YouTubePlaylistMetadata(
            summary.ExternalPlaylistId,
            summary.Name,
            summary.Url,
            string.IsNullOrWhiteSpace(publishedAt) ? summary.ExternalPlaylistId : $"{summary.ExternalPlaylistId}:{publishedAt}");
    }

    private async Task<IReadOnlyList<YouTubePlaylistItemRecord>> GetPlaylistItemsAsync(
        ExternalAccountId accountId,
        string externalPlaylistId,
        CancellationToken cancellationToken)
    {
        var items = new List<YouTubePlaylistItemRecord>();
        string? pageToken = null;

        do
        {
            var query = new Dictionary<string, string>
            {
                ["part"] = "snippet,contentDetails,status",
                ["playlistId"] = externalPlaylistId,
                ["maxResults"] = MaxPageSize.ToString(CultureInfo.InvariantCulture),
            };
            if (!string.IsNullOrWhiteSpace(pageToken))
                query["pageToken"] = pageToken;

            using var response = await SendAuthorizedAsync(
                accountId,
                accessToken => CreateBearerRequest(HttpMethod.Get, BuildApiUri("playlistItems", query), accessToken),
                cancellationToken);
            using var document = await ReadSuccessDocumentAsync(response, cancellationToken);
            var root = document.RootElement;
            foreach (var item in EnumerateItems(root))
                items.Add(MapPendingPlaylistItem(item, items.Count));
            pageToken = GetString(root, "nextPageToken");
        }
        while (!string.IsNullOrWhiteSpace(pageToken));

        return items;
    }

    private async Task<IReadOnlyDictionary<string, YouTubeVideoMetadata>> GetVideoMetadataAsync(
        ExternalAccountId accountId,
        IReadOnlyList<YouTubePlaylistItemRecord> items,
        CancellationToken cancellationToken)
    {
        var ids = items
            .Select(item => item.VideoId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select(id => id!)
            .ToArray();
        if (ids.Length == 0)
            return new Dictionary<string, YouTubeVideoMetadata>(StringComparer.Ordinal);

        var result = new Dictionary<string, YouTubeVideoMetadata>(StringComparer.Ordinal);
        foreach (var batch in ids.Chunk(MaxPageSize))
        {
            using var response = await SendAuthorizedAsync(
                accountId,
                accessToken => CreateBearerRequest(HttpMethod.Get, BuildApiUri("videos", new Dictionary<string, string>
                {
                    ["part"] = "snippet,contentDetails,status",
                    ["id"] = string.Join(',', batch),
                    ["maxResults"] = MaxPageSize.ToString(CultureInfo.InvariantCulture),
                }), accessToken),
                cancellationToken);
            using var document = await ReadSuccessDocumentAsync(response, cancellationToken);
            foreach (var item in EnumerateItems(document.RootElement))
            {
                var id = GetString(item, "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var snippet = GetObject(item, "snippet");
                var contentDetails = GetObject(item, "contentDetails");
                var status = GetObject(item, "status");
                result[id] = new YouTubeVideoMetadata(
                    id,
                    GetString(snippet, "title"),
                    GetString(snippet, "channelTitle"),
                    ParseYouTubeDurationMs(GetString(contentDetails, "duration")),
                    GetString(status, "privacyStatus"),
                    item.GetRawText());
            }
        }

        return result;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        ExternalAccountId accountId,
        Func<string, HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        var account = RequireAccount(accountId);
        var accessToken = await ReadAccessTokenAsync(account, cancellationToken);
        using var request = requestFactory(accessToken);
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        var refreshedToken = await RefreshAccessTokenAsync(account, cancellationToken);
        using var refreshedRequest = requestFactory(refreshedToken);
        return await httpClient.SendAsync(refreshedRequest, cancellationToken);
    }

    private async Task<string> ReadAccessTokenAsync(
        YouTubeAccountRecord account,
        CancellationToken cancellationToken)
    {
        var secret = await secretStore.ReadAsync(account.SecretReference, cancellationToken)
            ?? throw new YouTubeProviderException("YouTube account credential was not found.", HttpStatusCode.Unauthorized, reauthorizationRequired: true);
        if (!secret.Secrets.TryGetValue("access_token", out var accessToken) || string.IsNullOrWhiteSpace(accessToken))
            throw new YouTubeProviderException("YouTube access token is missing from the local secret store.", HttpStatusCode.Unauthorized, reauthorizationRequired: true);
        return accessToken;
    }

    private async Task<string> RefreshAccessTokenAsync(
        YouTubeAccountRecord account,
        CancellationToken cancellationToken)
    {
        await account.RefreshGate.WaitAsync(cancellationToken);
        try
        {
            var currentSecretReference = account.SecretReference;
            var existing = await secretStore.ReadAsync(currentSecretReference, cancellationToken)
                ?? throw new YouTubeProviderException("YouTube account credential was not found.", HttpStatusCode.Unauthorized, reauthorizationRequired: true);
            if (!existing.Secrets.TryGetValue("refresh_token", out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
                throw new YouTubeProviderException("YouTube authorization expired and cannot be refreshed. Reconnect YouTube.", HttpStatusCode.Unauthorized, reauthorizationRequired: true);

            try
            {
                var tokenResponse = await PostTokenAsync(
                    new Dictionary<string, string>
                    {
                        ["grant_type"] = "refresh_token",
                        ["refresh_token"] = refreshToken,
                        ["client_id"] = options.ClientId,
                    },
                    cancellationToken);
                var newSecretReference = await SaveTokenSecretAsync(tokenResponse, refreshToken, cancellationToken);
                await secretStore.DeleteAsync(currentSecretReference, cancellationToken);

                lock (gate)
                    account.SecretReference = newSecretReference;

                return tokenResponse.AccessToken;
            }
            catch (YouTubeProviderException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                throw new YouTubeProviderException(
                    "YouTube authorization was revoked or expired. Reconnect YouTube.",
                    HttpStatusCode.Unauthorized,
                    reauthorizationRequired: true,
                    innerException: ex);
            }
        }
        finally
        {
            account.RefreshGate.Release();
        }
    }

    private async Task<YouTubeTokenResponse> PostTokenAsync(
        IReadOnlyDictionary<string, string> formFields,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpointUri)
        {
            Content = new FormUrlEncodedContent(formFields),
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        using var document = await ReadSuccessDocumentAsync(response, cancellationToken);
        var root = document.RootElement;
        return new YouTubeTokenResponse(
            GetRequiredString(root, "access_token"),
            GetString(root, "refresh_token"),
            GetString(root, "token_type") ?? "Bearer",
            GetInt(root, "expires_in"),
            GetString(root, "scope"));
    }

    private async Task<string> SaveTokenSecretAsync(
        YouTubeTokenResponse tokenResponse,
        string? fallbackRefreshToken,
        CancellationToken cancellationToken)
    {
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["access_token"] = tokenResponse.AccessToken,
            ["token_type"] = tokenResponse.TokenType,
        };
        var refreshToken = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
            ? fallbackRefreshToken
            : tokenResponse.RefreshToken;
        if (!string.IsNullOrWhiteSpace(refreshToken))
            secrets["refresh_token"] = refreshToken;
        if (!string.IsNullOrWhiteSpace(tokenResponse.Scope))
            secrets["scope"] = tokenResponse.Scope;

        var expiresAtUtc = tokenResponse.ExpiresInSeconds is > 0
            ? DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds.Value)
            : (DateTimeOffset?)null;
        return await secretStore.SaveAsync(new SecretStoreSaveRequest(
            ProviderId,
            secrets,
            expiresAtUtc), cancellationToken);
    }

    private async Task<YouTubeChannelProfile> GetCurrentChannelProfileAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateBearerRequest(HttpMethod.Get, BuildApiUri("channels", new Dictionary<string, string>
        {
            ["part"] = "snippet",
            ["mine"] = "true",
            ["maxResults"] = "1",
        }), accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        using var document = await ReadSuccessDocumentAsync(response, cancellationToken);
        var channel = EnumerateItems(document.RootElement).FirstOrDefault();
        if (channel.ValueKind == JsonValueKind.Undefined)
            throw new YouTubeProviderException("YouTube account did not return an owned channel.", HttpStatusCode.Forbidden);

        return new YouTubeChannelProfile(
            GetRequiredString(channel, "id"),
            GetString(GetObject(channel, "snippet"), "title"));
    }

    private static HttpRequestMessage CreateBearerRequest(
        HttpMethod method,
        Uri uri,
        string accessToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private async Task<JsonDocument> ReadSuccessDocumentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }

        var retryAfter = GetRetryAfter(response);
        var message = await ReadErrorMessageAsync(response, cancellationToken);
        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new YouTubeProviderException(
                message ?? "YouTube authorization expired. Reconnect YouTube.",
                response.StatusCode,
                retryAfter,
                reauthorizationRequired: true),
            HttpStatusCode.BadRequest => new YouTubeProviderException(
                message ?? "YouTube request was rejected.",
                response.StatusCode,
                retryAfter),
            HttpStatusCode.Forbidden => new YouTubeProviderException(
                message ?? "YouTube quota, policy or permission check rejected the request.",
                response.StatusCode,
                retryAfter),
            HttpStatusCode.TooManyRequests => new YouTubeProviderException(
                message ?? "YouTube rate limit was reached. Try again after the Retry-After interval.",
                response.StatusCode,
                retryAfter),
            _ => new YouTubeProviderException(
                message ?? $"YouTube request failed with HTTP {(int)response.StatusCode}.",
                response.StatusCode,
                retryAfter),
        };
    }

    private static async Task<string?> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString();

                if (error.ValueKind != JsonValueKind.Object)
                    return null;

                var message = GetString(error, "message");
                var reason = GetFirstGoogleErrorReason(error);
                if (!string.IsNullOrWhiteSpace(reason) && !string.IsNullOrWhiteSpace(message))
                    return $"YouTube {reason}: {message}";
                if (!string.IsNullOrWhiteSpace(message))
                    return message;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? GetFirstGoogleErrorReason(JsonElement error)
    {
        if (!error.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in errors.EnumerateArray())
        {
            var reason = GetString(item, "reason");
            if (!string.IsNullOrWhiteSpace(reason))
                return reason;
        }

        return null;
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return delta;
        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }

    private ExternalPlaylistSummary MapPlaylistSummary(JsonElement item)
    {
        var id = GetRequiredString(item, "id");
        return new ExternalPlaylistSummary(
            ProviderId,
            id,
            GetString(GetObject(item, "snippet"), "title") ?? "YouTube playlist",
            BuildPlaylistUrl(id),
            GetInt(GetObject(item, "contentDetails"), "itemCount"),
            TryParseDateTimeOffset(GetString(GetObject(item, "snippet"), "publishedAt")));
    }

    private YouTubePlaylistItemRecord MapPendingPlaylistItem(JsonElement item, int fallbackPosition)
    {
        var snippet = GetObject(item, "snippet");
        var contentDetails = GetObject(item, "contentDetails");
        var resourceId = GetObject(snippet, "resourceId");
        var videoId = GetString(resourceId, "videoId") ?? GetString(contentDetails, "videoId");
        return new YouTubePlaylistItemRecord(
            GetString(item, "id"),
            videoId,
            GetInt(snippet, "position") ?? fallbackPosition,
            GetString(snippet, "title"),
            GetString(snippet, "channelTitle"),
            GetString(snippet, "videoOwnerChannelTitle"),
            GetBestThumbnailUrl(snippet),
            GetString(GetObject(item, "status"), "privacyStatus"),
            item.GetRawText());
    }

    private ExternalTrackSnapshot MapPlaylistItem(
        string playlistId,
        YouTubePlaylistItemRecord item,
        IReadOnlyDictionary<string, YouTubeVideoMetadata> videoMetadata,
        int fallbackPosition,
        Dictionary<string, int> occurrenceCounts)
    {
        videoMetadata.TryGetValue(item.VideoId ?? string.Empty, out var metadata);
        var videoId = item.VideoId;
        var externalId = string.IsNullOrWhiteSpace(videoId)
            ? $"youtube:unavailable:{playlistId}:{fallbackPosition}"
            : videoId;
        var title = ChooseTitle(item, metadata);
        var channel = metadata?.ChannelTitle
            ?? item.VideoOwnerChannelTitle
            ?? item.ChannelTitle;
        var identity = item.PlaylistItemId
            ?? (string.IsNullOrWhiteSpace(videoId) ? externalId : $"youtube:video:{videoId}");

        return new ExternalTrackSnapshot(
            ProviderId,
            externalId,
            BuildProviderItemId(identity, occurrenceCounts),
            item.Position,
            title,
            string.IsNullOrWhiteSpace(channel) ? [] : [channel],
            null,
            metadata?.DurationMs,
            null,
            string.IsNullOrWhiteSpace(videoId) ? null : BuildWatchUrl(videoId),
            item.ThumbnailUrl,
            null,
            BuildRawMetadataJson(item.RawJson, metadata?.RawJson));
    }

    private static string ChooseTitle(
        YouTubePlaylistItemRecord item,
        YouTubeVideoMetadata? metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata?.Title))
            return metadata.Title;
        if (!string.IsNullOrWhiteSpace(item.Title))
            return item.Title;
        if (StringComparer.OrdinalIgnoreCase.Equals(item.PrivacyStatus, "private"))
            return "Private YouTube video";
        return "Unavailable YouTube video";
    }

    private static IEnumerable<JsonElement> EnumerateItems(JsonElement page)
    {
        if (!page.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var item in items.EnumerateArray())
            yield return item;
    }

    private static string BuildProviderItemId(
        string identity,
        Dictionary<string, int> occurrenceCounts)
    {
        occurrenceCounts.TryGetValue(identity, out var count);
        count++;
        occurrenceCounts[identity] = count;
        return count == 1
            ? identity
            : string.Create(CultureInfo.InvariantCulture, $"{identity}#{count}");
    }

    private YouTubeAccountRecord RequireAccount(ExternalAccountId accountId)
    {
        lock (gate)
        {
            if (accounts.TryGetValue(accountId.Value, out var account))
                return account;
        }

        throw new YouTubeProviderException("YouTube account is not connected.", HttpStatusCode.Unauthorized, reauthorizationRequired: true);
    }

    private void ValidateProvider(string providerId, string parameterName)
    {
        if (!StringComparer.Ordinal.Equals(providerId, ProviderId))
            throw new ArgumentException("Request is for a different provider.", parameterName);
    }

    private Uri BuildApiUri(
        string relativePath,
        IReadOnlyDictionary<string, string> query)
        => BuildUri(new Uri(options.ApiBaseUri, relativePath.TrimStart('/')), query);

    private static Uri BuildUri(
        Uri uri,
        IReadOnlyDictionary<string, string> query)
    {
        var builder = new UriBuilder(uri)
        {
            Query = string.Join('&', query.Select(pair =>
                Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value))),
        };
        return builder.Uri;
    }

    private static string BuildPlaylistUrl(string playlistId)
        => "https://www.youtube.com/playlist?list=" + Uri.EscapeDataString(playlistId);

    private static string BuildWatchUrl(string videoId)
        => "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(videoId);

    private static string? GetBestThumbnailUrl(JsonElement? snippet)
    {
        if (GetObject(snippet, "thumbnails") is not { } thumbnails)
            return null;

        foreach (var name in new[] { "maxres", "standard", "high", "medium", "default" })
        {
            var url = GetString(GetObject(thumbnails, name), "url");
            if (!string.IsNullOrWhiteSpace(url))
                return url;
        }

        return null;
    }

    private static string BuildRawMetadataJson(string playlistItemJson, string? videoJson)
        => JsonSerializer.Serialize(new
        {
            playlistItem = playlistItemJson,
            video = videoJson,
        });

    private static int? ParseYouTubeDurationMs(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return null;

        try
        {
            var milliseconds = XmlConvert.ToTimeSpan(duration).TotalMilliseconds;
            if (milliseconds < 0 || milliseconds > int.MaxValue)
                return null;
            return (int)Math.Round(milliseconds, MidpointRounding.AwayFromZero);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static DateTimeOffset? TryParseDateTimeOffset(string? value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    private static long ComputeSnapshotVersion(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt64(hash, 0) & long.MaxValue;
    }

    private static JsonElement? GetObject(JsonElement? element, string name)
    {
        if (element is not { } value || !value.TryGetProperty(name, out var child) || child.ValueKind != JsonValueKind.Object)
            return null;
        return child;
    }

    private static string GetRequiredString(JsonElement element, string name)
        => GetString(element, name)
            ?? throw new YouTubeProviderException($"YouTube response did not include required field '{name}'.");

    private static string? GetString(JsonElement? element, string name)
    {
        if (element is not { } value || !value.TryGetProperty(name, out var child))
            return null;
        return child.ValueKind == JsonValueKind.String ? child.GetString() : null;
    }

    private static int? GetInt(JsonElement? element, string name)
    {
        if (element is not { } value || !value.TryGetProperty(name, out var child))
            return null;
        return child.ValueKind == JsonValueKind.Number && child.TryGetInt32(out var number) ? number : null;
    }

    private sealed class YouTubeAccountRecord(
        ExternalAccountId accountId,
        string externalUserId,
        string displayName,
        string secretReference,
        DateTimeOffset authorizedAtUtc)
    {
        public ExternalAccountId AccountId { get; } = accountId;

        public string ExternalUserId { get; } = externalUserId;

        public string DisplayName { get; } = displayName;

        public string SecretReference { get; set; } = secretReference;

        public DateTimeOffset AuthorizedAtUtc { get; } = authorizedAtUtc;

        public SemaphoreSlim RefreshGate { get; } = new(1, 1);
    }

    private sealed record YouTubeTokenResponse(
        string AccessToken,
        string? RefreshToken,
        string TokenType,
        int? ExpiresInSeconds,
        string? Scope);

    private sealed record YouTubeChannelProfile(
        string ChannelId,
        string? Title);

    private sealed record YouTubePlaylistMetadata(
        string ExternalPlaylistId,
        string Name,
        string? Url,
        string SnapshotIdentity);

    private sealed record YouTubePlaylistItemRecord(
        string? PlaylistItemId,
        string? VideoId,
        int Position,
        string? Title,
        string? ChannelTitle,
        string? VideoOwnerChannelTitle,
        string? ThumbnailUrl,
        string? PrivacyStatus,
        string RawJson);

    private sealed record YouTubeVideoMetadata(
        string VideoId,
        string? Title,
        string? ChannelTitle,
        int? DurationMs,
        string? PrivacyStatus,
        string RawJson);
}
