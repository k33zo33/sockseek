using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sockseek.Application.Security;
using Sockseek.Integrations.Abstractions;

namespace Sockseek.Integrations.Spotify;

public sealed class SpotifyPlaylistSourceProvider : IPlaylistSourceProvider
{
    private readonly HttpClient httpClient;
    private readonly ISecretStore secretStore;
    private readonly SpotifyPlaylistSourceOptions options;
    private readonly Dictionary<Guid, SpotifyAccountRecord> accounts = new();
    private readonly object gate = new();

    public SpotifyPlaylistSourceProvider(
        HttpClient httpClient,
        ISecretStore secretStore,
        SpotifyPlaylistSourceOptions options)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        this.options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
    }

    public string ProviderId => ProviderIds.Spotify;

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
            accounts[account.AccountId.Value] = new SpotifyAccountRecord(
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
        };

        return Task.FromResult(new AuthorizationStartResult(
            BuildUri(options.AccountsBaseUri, "authorize", query),
            request.State));
    }

    public async Task<ExternalAccountSnapshot> CompleteAuthorizationAsync(
        AuthorizationCallback callback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ValidateProvider(callback.ProviderId, nameof(callback));
        if (!string.IsNullOrWhiteSpace(callback.Error))
            throw new SpotifyProviderException($"Spotify authorization failed: {callback.Error}");
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

        var profile = await GetCurrentUserProfileAsync(tokenResponse.AccessToken, cancellationToken);
        var secretReference = await SaveTokenSecretAsync(tokenResponse, null, cancellationToken);
        var account = new SpotifyAccountRecord(
            new ExternalAccountId(Guid.NewGuid()),
            profile.Id,
            string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Id : profile.DisplayName,
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
        var next = BuildUri(options.ApiBaseUri, "me/playlists", new Dictionary<string, string>
        {
            ["limit"] = "50",
        });

        while (next != null)
        {
            using var response = await SendAuthorizedAsync(
                accountId,
                accessToken => CreateBearerRequest(HttpMethod.Get, next, accessToken),
                cancellationToken);
            using var document = await ReadSuccessDocumentAsync(response, cancellationToken);
            var root = document.RootElement;
            foreach (var item in EnumerateItems(root))
                playlists.Add(MapPlaylistSummary(item));
            next = GetNextPage(root);
        }

        return playlists;
    }

    public async Task<ExternalPlaylistSnapshot> GetPlaylistAsync(
        ExternalPlaylistRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateProvider(request.ProviderId, nameof(request));
        if (request.AccountId is not { } accountId)
            throw new ArgumentException("Spotify playlist import requires a connected account.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExternalPlaylistId);

        var playlistUri = BuildUri(options.ApiBaseUri, $"playlists/{Uri.EscapeDataString(request.ExternalPlaylistId)}");
        using var playlistResponse = await SendAuthorizedAsync(
            accountId,
            accessToken => CreateBearerRequest(HttpMethod.Get, playlistUri, accessToken),
            cancellationToken);
        using var playlistDocument = await ReadSuccessDocumentAsync(playlistResponse, cancellationToken);
        var playlistRoot = playlistDocument.RootElement;

        var items = new List<ExternalTrackSnapshot>();
        var occurrenceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var next = BuildUri(options.ApiBaseUri, $"playlists/{Uri.EscapeDataString(request.ExternalPlaylistId)}/tracks", new Dictionary<string, string>
        {
            ["limit"] = "100",
        });

        while (next != null)
        {
            using var response = await SendAuthorizedAsync(
                accountId,
                accessToken => CreateBearerRequest(HttpMethod.Get, next, accessToken),
                cancellationToken);
            using var document = await ReadSuccessDocumentAsync(response, cancellationToken);
            var root = document.RootElement;
            foreach (var item in EnumerateItems(root))
                items.Add(MapPlaylistItem(request.ExternalPlaylistId, item, items.Count, occurrenceCounts));
            next = GetNextPage(root);
        }

        var snapshotId = GetString(playlistRoot, "snapshot_id");
        return new ExternalPlaylistSnapshot(
            ProviderId,
            GetString(playlistRoot, "id") ?? request.ExternalPlaylistId,
            GetString(playlistRoot, "name") ?? "Spotify playlist",
            GetExternalSpotifyUrl(playlistRoot),
            ComputeSnapshotVersion(snapshotId ?? request.ExternalPlaylistId),
            DateTimeOffset.UtcNow,
            items);
    }

    public async Task DisconnectAsync(
        ExternalAccountId accountId,
        CancellationToken cancellationToken)
    {
        SpotifyAccountRecord? account;
        lock (gate)
            accounts.Remove(accountId.Value, out account);

        if (account != null)
            await secretStore.DeleteAsync(account.SecretReference, cancellationToken);
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
        SpotifyAccountRecord account,
        CancellationToken cancellationToken)
    {
        var secret = await secretStore.ReadAsync(account.SecretReference, cancellationToken)
            ?? throw new SpotifyProviderException("Spotify account credential was not found.", HttpStatusCode.Unauthorized, reauthorizationRequired: true);
        if (!secret.Secrets.TryGetValue("access_token", out var accessToken) || string.IsNullOrWhiteSpace(accessToken))
            throw new SpotifyProviderException("Spotify access token is missing from the local secret store.", HttpStatusCode.Unauthorized, reauthorizationRequired: true);
        return accessToken;
    }

    private async Task<string> RefreshAccessTokenAsync(
        SpotifyAccountRecord account,
        CancellationToken cancellationToken)
    {
        await account.RefreshGate.WaitAsync(cancellationToken);
        try
        {
            var currentSecretReference = account.SecretReference;
            var existing = await secretStore.ReadAsync(currentSecretReference, cancellationToken)
                ?? throw new SpotifyProviderException("Spotify account credential was not found.", HttpStatusCode.Unauthorized, reauthorizationRequired: true);
            if (!existing.Secrets.TryGetValue("refresh_token", out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
                throw new SpotifyProviderException("Spotify authorization expired and cannot be refreshed. Reconnect Spotify.", HttpStatusCode.Unauthorized, reauthorizationRequired: true);

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
        finally
        {
            account.RefreshGate.Release();
        }
    }

    private async Task<SpotifyTokenResponse> PostTokenAsync(
        IReadOnlyDictionary<string, string> formFields,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(options.AccountsBaseUri, "api/token"))
        {
            Content = new FormUrlEncodedContent(formFields),
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        using var document = await ReadSuccessDocumentAsync(response, cancellationToken);
        var root = document.RootElement;
        return new SpotifyTokenResponse(
            GetRequiredString(root, "access_token"),
            GetString(root, "refresh_token"),
            GetString(root, "token_type") ?? "Bearer",
            GetInt(root, "expires_in"),
            GetString(root, "scope"));
    }

    private async Task<string> SaveTokenSecretAsync(
        SpotifyTokenResponse tokenResponse,
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

    private async Task<SpotifyUserProfile> GetCurrentUserProfileAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateBearerRequest(HttpMethod.Get, BuildUri(options.ApiBaseUri, "me"), accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        using var document = await ReadSuccessDocumentAsync(response, cancellationToken);
        var root = document.RootElement;
        return new SpotifyUserProfile(
            GetRequiredString(root, "id"),
            GetString(root, "display_name"));
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
        if (response.StatusCode == HttpStatusCode.BadRequest
            && string.Equals(message, "invalid_grant", StringComparison.OrdinalIgnoreCase))
        {
            throw new SpotifyProviderException(
                "Spotify authorization expired and cannot be refreshed. Reconnect Spotify.",
                response.StatusCode,
                retryAfter,
                reauthorizationRequired: true);
        }

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new SpotifyProviderException(
                message ?? "Spotify authorization expired. Reconnect Spotify.",
                response.StatusCode,
                reauthorizationRequired: true),
            HttpStatusCode.Forbidden => new SpotifyProviderException(
                message ?? "This Spotify account is not allowlisted for the development-mode app. Ask the app owner to add the account in Spotify Developer Dashboard.",
                response.StatusCode),
            HttpStatusCode.TooManyRequests => new SpotifyProviderException(
                message ?? "Spotify rate limit was reached. Try again after the Retry-After interval.",
                response.StatusCode,
                retryAfter),
            _ => new SpotifyProviderException(
                message ?? $"Spotify request failed with HTTP {(int)response.StatusCode}.",
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
                if (error.ValueKind == JsonValueKind.Object)
                {
                    var message = GetString(error, "message");
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        if (response.StatusCode == HttpStatusCode.Forbidden)
                            return "Spotify development-mode app rejected this account. Ask the app owner to allowlist it in Spotify Developer Dashboard.";
                        return message;
                    }
                }

                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
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
        => new(
            ProviderId,
            GetRequiredString(item, "id"),
            GetString(item, "name") ?? "Spotify playlist",
            GetExternalSpotifyUrl(item),
            GetObject(item, "tracks") is { } tracks ? GetInt(tracks, "total") : null,
            null);

    private ExternalTrackSnapshot MapPlaylistItem(
        string playlistId,
        JsonElement item,
        int position,
        Dictionary<string, int> occurrenceCounts)
    {
        var rawJson = item.GetRawText();
        if (GetObject(item, "track") is not { } track)
            return CreateUnsupportedItem(playlistId, position, occurrenceCounts, rawJson, "Unavailable Spotify item");

        var type = GetString(track, "type");
        var isLocal = GetBool(track, "is_local") ?? false;
        var externalId = GetString(track, "id") ?? GetString(track, "uri");
        var externalUrl = GetExternalSpotifyUrl(track);
        var title = GetString(track, "name");
        var artists = GetArtists(track);
        var durationMs = GetInt(track, "duration_ms");
        var providerIdentity = GetString(track, "uri") ?? externalId ?? $"{playlistId}:position:{position}";
        var providerItemId = BuildProviderItemId(providerIdentity, occurrenceCounts);

        if (StringComparer.Ordinal.Equals(type, "episode"))
        {
            var show = GetObject(track, "show");
            var showName = show is { } showElement ? GetString(showElement, "name") : null;
            var publisher = show is { } publisherElement ? GetString(publisherElement, "publisher") : null;
            return new ExternalTrackSnapshot(
                ProviderId,
                externalId ?? $"spotify:episode:{playlistId}:{position}",
                providerItemId,
                position,
                title ?? "Spotify episode",
                string.IsNullOrWhiteSpace(publisher) ? [] : [publisher],
                showName,
                durationMs,
                null,
                externalUrl,
                GetBestImageUrl(track) ?? (show is { } showArtwork ? GetBestImageUrl(showArtwork) : null),
                null,
                rawJson);
        }

        if (!StringComparer.Ordinal.Equals(type, "track") || isLocal)
        {
            return new ExternalTrackSnapshot(
                ProviderId,
                externalId ?? $"spotify:unsupported:{playlistId}:{position}",
                providerItemId,
                position,
                title ?? "Unsupported Spotify item",
                artists,
                GetString(GetObject(track, "album"), "name"),
                durationMs,
                null,
                externalUrl,
                GetBestImageUrl(GetObject(track, "album")),
                null,
                rawJson);
        }

        return new ExternalTrackSnapshot(
            ProviderId,
            externalId ?? $"spotify:unavailable:{playlistId}:{position}",
            providerItemId,
            position,
            title ?? "Unavailable Spotify track",
            artists,
            GetString(GetObject(track, "album"), "name"),
            durationMs,
            GetString(GetObject(track, "external_ids"), "isrc"),
            externalUrl,
            GetBestImageUrl(GetObject(track, "album")),
            null,
            rawJson);
    }

    private ExternalTrackSnapshot CreateUnsupportedItem(
        string playlistId,
        int position,
        Dictionary<string, int> occurrenceCounts,
        string rawJson,
        string title)
    {
        var identity = $"spotify:unsupported:{playlistId}:{position}";
        return new ExternalTrackSnapshot(
            ProviderId,
            identity,
            BuildProviderItemId(identity, occurrenceCounts),
            position,
            title,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            rawJson);
    }

    private static IEnumerable<JsonElement> EnumerateItems(JsonElement page)
    {
        if (!page.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var item in items.EnumerateArray())
            yield return item;
    }

    private static Uri? GetNextPage(JsonElement page)
    {
        var next = GetString(page, "next");
        return string.IsNullOrWhiteSpace(next) ? null : new Uri(next, UriKind.Absolute);
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

    private static IReadOnlyList<string> GetArtists(JsonElement track)
    {
        if (!track.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array)
            return [];

        return artists.EnumerateArray()
            .Select(artist => GetString(artist, "name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToArray();
    }

    private static string? GetBestImageUrl(JsonElement? container)
    {
        if (container is not { } element || !element.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
            return null;

        return images.EnumerateArray()
            .Select(image => new
            {
                Url = GetString(image, "url"),
                Width = GetInt(image, "width") ?? 0,
                Height = GetInt(image, "height") ?? 0,
            })
            .Where(image => !string.IsNullOrWhiteSpace(image.Url))
            .OrderByDescending(image => image.Width * image.Height)
            .Select(image => image.Url)
            .FirstOrDefault();
    }

    private SpotifyAccountRecord RequireAccount(ExternalAccountId accountId)
    {
        lock (gate)
        {
            if (accounts.TryGetValue(accountId.Value, out var account))
                return account;
        }

        throw new SpotifyProviderException("Spotify account is not connected.", HttpStatusCode.Unauthorized);
    }

    private void ValidateProvider(string providerId, string parameterName)
    {
        if (!StringComparer.Ordinal.Equals(providerId, ProviderId))
            throw new ArgumentException("Request is for a different provider.", parameterName);
    }

    private static Uri BuildUri(Uri baseUri, string relativePath)
        => new(baseUri, relativePath.TrimStart('/'));

    private static Uri BuildUri(
        Uri baseUri,
        string relativePath,
        IReadOnlyDictionary<string, string> query)
    {
        var uri = BuildUri(baseUri, relativePath);
        var builder = new UriBuilder(uri)
        {
            Query = string.Join('&', query.Select(pair =>
                Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value))),
        };
        return builder.Uri;
    }

    private static string? GetExternalSpotifyUrl(JsonElement element)
        => GetString(GetObject(element, "external_urls"), "spotify");

    private static JsonElement? GetObject(JsonElement? element, string name)
    {
        if (element is not { } value || !value.TryGetProperty(name, out var child) || child.ValueKind != JsonValueKind.Object)
            return null;
        return child;
    }

    private static string GetRequiredString(JsonElement element, string name)
        => GetString(element, name)
            ?? throw new SpotifyProviderException($"Spotify response did not include required field '{name}'.");

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

    private static bool? GetBool(JsonElement? element, string name)
    {
        if (element is not { } value || !value.TryGetProperty(name, out var child))
            return null;
        return child.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static long ComputeSnapshotVersion(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt64(hash, 0) & long.MaxValue;
    }

    private sealed class SpotifyAccountRecord(
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

    private sealed record SpotifyTokenResponse(
        string AccessToken,
        string? RefreshToken,
        string TokenType,
        int? ExpiresInSeconds,
        string? Scope);

    private sealed record SpotifyUserProfile(
        string Id,
        string? DisplayName);
}
