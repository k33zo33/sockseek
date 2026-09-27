using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Sockseek.Integrations.Abstractions;

namespace Sockseek.Integrations.Bandcamp;

public sealed partial class BandcampPlaylistSourceProvider(HttpClient httpClient) : IPlaylistSourceProvider
{
    private readonly HttpClient httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public string ProviderId => ProviderIds.Bandcamp;

    public PlaylistProviderCapabilities Capabilities =>
        PlaylistProviderCapabilities.ImportPublicUrl
        | PlaylistProviderCapabilities.ReadPlaylistItems;

    public Task<AuthorizationStartResult> StartAuthorizationAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Bandcamp public URL import does not use account authorization.");

    public Task<ExternalAccountSnapshot> CompleteAuthorizationAsync(
        AuthorizationCallback callback,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Bandcamp public URL import does not use account authorization.");

    public Task<IReadOnlyList<ExternalPlaylistSummary>> GetPlaylistsAsync(
        ExternalAccountId accountId,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Bandcamp does not expose connected-account playlists in Sockseek.");

    public async Task<ExternalPlaylistSnapshot> GetPlaylistAsync(
        ExternalPlaylistRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateProvider(request.ProviderId, nameof(request));

        var url = ResolvePublicUrl(request);
        ValidateBandcampPublicUrl(url);

        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new BandcampProviderException(
                $"Bandcamp public URL import failed with HTTP {(int)response.StatusCode}.",
                response.StatusCode);

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = BandcampStructuredDataParser.Parse(html, url);
        return document.ToSnapshot();
    }

    public Task DisconnectAsync(
        ExternalAccountId accountId,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    private void ValidateProvider(string providerId, string parameterName)
    {
        if (!StringComparer.Ordinal.Equals(providerId, ProviderId))
            throw new ArgumentException("Request is for a different provider.", parameterName);
    }

    private static Uri ResolvePublicUrl(ExternalPlaylistRequest request)
    {
        var value = request.Url ?? request.ExternalPlaylistId;
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var url))
            throw new ArgumentException("Bandcamp import requires an absolute public URL.", nameof(request));
        return url;
    }

    private static void ValidateBandcampPublicUrl(Uri url)
    {
        if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
            throw new BandcampProviderException("Bandcamp import URL must use HTTP or HTTPS.");
        if (!url.Host.EndsWith("bandcamp.com", StringComparison.OrdinalIgnoreCase))
            throw new BandcampProviderException("Bandcamp import URL must be hosted on bandcamp.com.");
        if (!url.AbsolutePath.Contains("/album/", StringComparison.OrdinalIgnoreCase)
            && !url.AbsolutePath.Contains("/track/", StringComparison.OrdinalIgnoreCase))
        {
            throw new BandcampProviderException("Bandcamp import supports public album and track URLs only.");
        }
    }

    internal sealed record BandcampStructuredDocument(
        Uri SourceUrl,
        string Id,
        string Name,
        string Artist,
        string? ArtworkUrl,
        IReadOnlyList<BandcampStructuredTrack> Tracks)
    {
        public ExternalPlaylistSnapshot ToSnapshot()
            => new(
                ProviderIds.Bandcamp,
                Id,
                Name,
                SourceUrl.ToString(),
                ComputeSnapshotVersion(SourceUrl + ":" + string.Join('|', Tracks.Select(track => track.Id + ":" + track.Name))),
                DateTimeOffset.UtcNow,
                Tracks.Select((track, index) => new ExternalTrackSnapshot(
                    ProviderIds.Bandcamp,
                    track.Id,
                    track.Id,
                    index,
                    track.Name,
                    [string.IsNullOrWhiteSpace(track.Artist) ? Artist : track.Artist],
                    Name,
                    track.DurationMs,
                    null,
                    track.Url,
                    track.ArtworkUrl ?? ArtworkUrl,
                    null,
                    track.RawMetadataJson)).ToArray());
    }

    internal sealed record BandcampStructuredTrack(
        string Id,
        string Name,
        string Artist,
        string? Url,
        string? ArtworkUrl,
        int? DurationMs,
        string RawMetadataJson);

    internal static partial class BandcampStructuredDataParser
    {
        public static BandcampStructuredDocument Parse(string html, Uri sourceUrl)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(html);
            ArgumentNullException.ThrowIfNull(sourceUrl);

            foreach (Match match in JsonLdScriptRegex().Matches(html))
            {
                var json = WebUtility.HtmlDecode(match.Groups["json"].Value).Trim();
                if (TryParseJsonLd(json, sourceUrl, out var document))
                    return document;
            }

            throw new BandcampProviderException("Bandcamp public page did not contain supported structured metadata.");
        }

        private static bool TryParseJsonLd(
            string json,
            Uri sourceUrl,
            out BandcampStructuredDocument document)
        {
            document = null!;
            try
            {
                using var parsed = JsonDocument.Parse(json);
                foreach (var candidate in EnumerateCandidates(parsed.RootElement))
                {
                    var type = GetString(candidate, "@type");
                    if (StringComparer.OrdinalIgnoreCase.Equals(type, "MusicAlbum"))
                    {
                        document = ParseAlbum(candidate, sourceUrl);
                        return true;
                    }

                    if (StringComparer.OrdinalIgnoreCase.Equals(type, "MusicRecording"))
                    {
                        document = ParseTrackPage(candidate, sourceUrl);
                        return true;
                    }
                }
            }
            catch (JsonException ex)
            {
                throw new BandcampProviderException("Bandcamp structured metadata could not be parsed.", innerException: ex);
            }

            return false;
        }

        private static IEnumerable<JsonElement> EnumerateCandidates(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                    yield return item;
                yield break;
            }

            if (root.ValueKind == JsonValueKind.Object)
                yield return root;
        }

        private static BandcampStructuredDocument ParseAlbum(JsonElement album, Uri sourceUrl)
        {
            var name = GetString(album, "name") ?? throw new BandcampProviderException("Bandcamp album metadata did not include a name.");
            var artist = GetArtist(album) ?? "Unknown Artist";
            var artwork = GetString(album, "image");
            var url = GetString(album, "url") ?? sourceUrl.ToString();
            var tracks = GetObject(album, "track") is { } trackList
                ? ParseTrackList(trackList, artist, artwork).ToArray()
                : [];
            if (tracks.Length == 0)
                throw new BandcampProviderException("Bandcamp album metadata did not include any tracks.");

            return new BandcampStructuredDocument(
                new Uri(url, UriKind.Absolute),
                NormalizeExternalId(url),
                name,
                artist,
                artwork,
                tracks);
        }

        private static BandcampStructuredDocument ParseTrackPage(JsonElement track, Uri sourceUrl)
        {
            var name = GetString(track, "name") ?? throw new BandcampProviderException("Bandcamp track metadata did not include a name.");
            var artist = GetArtist(track) ?? "Unknown Artist";
            var url = GetString(track, "url") ?? sourceUrl.ToString();
            var artwork = GetString(track, "image");
            var item = new BandcampStructuredTrack(
                NormalizeExternalId(url),
                name,
                artist,
                url,
                artwork,
                ParseDurationMs(GetString(track, "duration")),
                track.GetRawText());

            return new BandcampStructuredDocument(
                new Uri(url, UriKind.Absolute),
                NormalizeExternalId(url),
                name,
                artist,
                artwork,
                [item]);
        }

        private static IEnumerable<BandcampStructuredTrack> ParseTrackList(
            JsonElement trackList,
            string albumArtist,
            string? albumArtwork)
        {
            var elements = GetArray(trackList, "itemListElement")
                ?? (trackList.ValueKind == JsonValueKind.Array ? trackList.EnumerateArray() : []);

            foreach (var listItem in elements)
            {
                var item = GetObject(listItem, "item") ?? listItem;
                var name = GetString(item, "name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var url = GetString(item, "url");
                var id = string.IsNullOrWhiteSpace(url)
                    ? NormalizeExternalId(name)
                    : NormalizeExternalId(url);
                yield return new BandcampStructuredTrack(
                    id,
                    name,
                    GetArtist(item) ?? albumArtist,
                    url,
                    GetString(item, "image") ?? albumArtwork,
                    ParseDurationMs(GetString(item, "duration")),
                    item.GetRawText());
            }
        }

        private static string? GetArtist(JsonElement element)
        {
            var artist = GetObject(element, "byArtist")
                ?? GetObject(element, "byArtist".ToLowerInvariant());
            return GetString(artist, "name") ?? GetString(element, "byArtist");
        }

        private static IEnumerable<JsonElement>? GetArray(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var child) || child.ValueKind != JsonValueKind.Array)
                return null;
            return child.EnumerateArray();
        }

        private static JsonElement? GetObject(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var child) || child.ValueKind != JsonValueKind.Object)
                return null;
            return child;
        }

        private static string? GetString(JsonElement? element, string name)
        {
            if (element is not { } value || !value.TryGetProperty(name, out var child))
                return null;
            return child.ValueKind == JsonValueKind.String ? child.GetString() : null;
        }

        private static int? ParseDurationMs(string? duration)
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

        private static string NormalizeExternalId(string value)
            => value.Trim().ToLowerInvariant();

        [GeneratedRegex("<script[^>]+type=[\"']application/ld\\+json[\"'][^>]*>(?<json>.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex JsonLdScriptRegex();
    }

    private static long ComputeSnapshotVersion(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt64(hash, 0) & long.MaxValue;
    }
}
