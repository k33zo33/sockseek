using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Sockseek.Integrations.MusicBrainz;

public sealed class MusicBrainzMetadataClient
{
    private readonly HttpClient httpClient;
    private readonly MusicBrainzClientOptions options;
    private readonly MusicBrainzRequestLimiter limiter;
    private readonly IMusicBrainzResponseCache cache;
    private readonly IMusicBrainzClock clock;
    private readonly IMusicBrainzDelay delay;

    public MusicBrainzMetadataClient(
        HttpClient httpClient,
        MusicBrainzClientOptions? options = null,
        MusicBrainzRequestLimiter? limiter = null,
        IMusicBrainzResponseCache? cache = null,
        IMusicBrainzClock? clock = null,
        IMusicBrainzDelay? delay = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.options = options ?? MusicBrainzClientOptions.Default;
        this.limiter = limiter ?? MusicBrainzRequestLimiter.Shared;
        this.cache = cache ?? new MusicBrainzMemoryCache();
        this.clock = clock ?? SystemMusicBrainzClock.Instance;
        this.delay = delay ?? TaskMusicBrainzDelay.Instance;

        this.httpClient.BaseAddress ??= this.options.BaseUri;
        this.httpClient.DefaultRequestHeaders.Accept.Clear();
        this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        this.httpClient.DefaultRequestHeaders.UserAgent.Clear();
        this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(this.options.UserAgent);
    }

    public async Task<IReadOnlyList<MusicBrainzRecordingMetadata>> LookupRecordingsByIsrcAsync(
        string isrc,
        CancellationToken cancellationToken = default)
    {
        string normalizedIsrc = NormalizeIsrc(isrc);
        var uri = BuildUri($"isrc/{Uri.EscapeDataString(normalizedIsrc)}", new Dictionary<string, string>
        {
            ["inc"] = "recordings+artist-credits+releases+isrcs",
            ["fmt"] = "json",
        });

        string json = await GetJsonAsync(uri, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("recordings", out var recordings)
            || recordings.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return ParseRecordings(recordings, normalizedIsrc, null);
    }

    public async Task<IReadOnlyList<MusicBrainzRecordingMetadata>> SearchRecordingsAsync(
        MusicBrainzRecordingSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var uri = BuildUri("recording", new Dictionary<string, string>
        {
            ["query"] = BuildRecordingSearchQuery(query),
            ["limit"] = Math.Clamp(query.Limit, 1, 25).ToString(CultureInfo.InvariantCulture),
            ["fmt"] = "json",
        });

        string json = await GetJsonAsync(uri, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("recordings", out var recordings)
            || recordings.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return ParseRecordings(recordings, null, query);
    }

    private async Task<string> GetJsonAsync(Uri uri, CancellationToken cancellationToken)
    {
        string cacheKey = uri.AbsoluteUri;
        if (cache.TryGet(cacheKey, clock.UtcNow, out string? cachedJson)
            && cachedJson != null)
            return cachedJson;

        for (int attempt = 0; ; attempt++)
        {
            await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable
                && attempt < options.MaxServiceUnavailableRetries)
            {
                await delay.DelayAsync(options.ServiceUnavailableRetryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!response.IsSuccessStatusCode)
                throw new MusicBrainzProviderException(
                    $"MusicBrainz request failed with HTTP {(int)response.StatusCode}.",
                    response.StatusCode);

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            cache.Set(cacheKey, json, clock.UtcNow.Add(options.CacheTtl));
            return json;
        }
    }

    private Uri BuildUri(string relativePath, IReadOnlyDictionary<string, string> query)
    {
        var builder = new UriBuilder(new Uri(options.BaseUri, relativePath));
        builder.Query = string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return builder.Uri;
    }

    private static IReadOnlyList<MusicBrainzRecordingMetadata> ParseRecordings(
        JsonElement recordings,
        string? lookupIsrc,
        MusicBrainzRecordingSearchQuery? searchQuery)
    {
        var result = new List<MusicBrainzRecordingMetadata>();
        foreach (var recording in recordings.EnumerateArray())
        {
            string? id = GetString(recording, "id");
            string? title = GetString(recording, "title");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                continue;

            var artists = GetArtistCredit(recording);
            var isrcs = GetStringArray(recording, "isrcs");
            if (!string.IsNullOrWhiteSpace(lookupIsrc)
                && !isrcs.Contains(lookupIsrc, StringComparer.Ordinal))
            {
                isrcs = [.. isrcs, lookupIsrc];
            }

            var metadata = new MusicBrainzRecordingMetadata(
                id.Trim(),
                title.Trim(),
                artists,
                GetReleaseTitle(recording),
                GetInt32(recording, "length"),
                isrcs,
                GetInt32(recording, "score"),
                ConfidenceScore: 0d,
                RawMetadataJson: recording.GetRawText());

            if (searchQuery != null)
                metadata = metadata with { ConfidenceScore = CalculateConfidence(metadata, searchQuery) };

            result.Add(metadata);
        }

        return result;
    }

    private static double CalculateConfidence(
        MusicBrainzRecordingMetadata recording,
        MusicBrainzRecordingSearchQuery query)
    {
        double score = 0d;
        string normalizedRecordingTitle = NormalizeForSearch(recording.Title);
        string normalizedQueryTitle = NormalizeForSearch(query.Title);
        if (normalizedRecordingTitle == normalizedQueryTitle)
        {
            score += 0.45d;
        }
        else if (normalizedRecordingTitle.Contains(normalizedQueryTitle, StringComparison.Ordinal)
            || normalizedQueryTitle.Contains(normalizedRecordingTitle, StringComparison.Ordinal))
        {
            score += 0.25d;
        }

        string normalizedQueryArtist = NormalizeForSearch(query.Artist);
        if (recording.Artists.Any(artist => NormalizeForSearch(artist) == normalizedQueryArtist))
        {
            score += 0.35d;
        }
        else if (recording.Artists.Any(artist => NormalizeForSearch(artist).Contains(normalizedQueryArtist, StringComparison.Ordinal)))
        {
            score += 0.20d;
        }

        if (recording.DurationMs.HasValue
            && query.DurationMs.HasValue
            && Math.Abs(recording.DurationMs.Value - query.DurationMs.Value) <= 10_000)
        {
            score += 0.15d;
        }

        if (recording.MusicBrainzScore >= 90)
            score += 0.05d;
        else if (recording.MusicBrainzScore >= 70)
            score += 0.03d;

        return Math.Min(1d, score);
    }

    private static string BuildRecordingSearchQuery(MusicBrainzRecordingSearchQuery query)
        => $"recording:\"{EscapeLucene(query.Title)}\" AND artist:\"{EscapeLucene(query.Artist)}\"";

    private static string EscapeLucene(string value)
        => value.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static IReadOnlyList<string> GetArtistCredit(JsonElement recording)
    {
        if (!recording.TryGetProperty("artist-credit", out var artistCredit)
            || artistCredit.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var artists = new List<string>();
        foreach (var credit in artistCredit.EnumerateArray())
        {
            string? artist = GetString(credit, "name");
            if (string.IsNullOrWhiteSpace(artist)
                && credit.TryGetProperty("artist", out var artistObject)
                && artistObject.ValueKind == JsonValueKind.Object)
            {
                artist = GetString(artistObject, "name");
            }

            if (!string.IsNullOrWhiteSpace(artist))
                artists.Add(artist.Trim());
        }

        return artists;
    }

    private static string? GetReleaseTitle(JsonElement recording)
    {
        if (!recording.TryGetProperty("releases", out var releases)
            || releases.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var release in releases.EnumerateArray())
        {
            string? title = GetString(release, "title");
            if (!string.IsNullOrWhiteSpace(title))
                return title.Trim();
        }

        return null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt32(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out int result)
            ? result
            : null;

    private static string NormalizeIsrc(string isrc)
    {
        if (string.IsNullOrWhiteSpace(isrc))
            throw new ArgumentException("ISRC is required.", nameof(isrc));

        string normalized = new(isrc.Trim().Where(char.IsLetterOrDigit).ToArray());
        if (normalized.Length == 0)
            throw new ArgumentException("ISRC is required.", nameof(isrc));

        return normalized.ToUpperInvariant();
    }

    private static string NormalizeForSearch(string value)
    {
        var chars = value.Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();

        return string.Join(' ', new string(chars)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}

public sealed record MusicBrainzClientOptions(
    Uri BaseUri,
    string UserAgent,
    TimeSpan CacheTtl,
    int MaxServiceUnavailableRetries,
    TimeSpan ServiceUnavailableRetryDelay)
{
    public static MusicBrainzClientOptions Default { get; } = new(
        new Uri("https://musicbrainz.org/ws/2/"),
        "Sockseek/1.0 ( https://github.com/k33zo33/sockseek )",
        TimeSpan.FromHours(24),
        1,
        TimeSpan.FromSeconds(2));
}

public sealed record MusicBrainzRecordingSearchQuery(
    string Artist,
    string Title,
    int? DurationMs,
    int Limit = 5);

public sealed record MusicBrainzRecordingMetadata(
    string MusicBrainzRecordingId,
    string Title,
    IReadOnlyList<string> Artists,
    string? ReleaseTitle,
    int? DurationMs,
    IReadOnlyList<string> Isrcs,
    int? MusicBrainzScore,
    double ConfidenceScore,
    string RawMetadataJson)
{
    public bool IsHighConfidenceEnrichment => ConfidenceScore >= 0.92d;
}

public sealed class MusicBrainzProviderException(string message, HttpStatusCode statusCode)
    : InvalidOperationException(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class MusicBrainzRequestLimiter(
    IMusicBrainzClock clock,
    IMusicBrainzDelay delay,
    TimeSpan? minimumInterval = null)
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly TimeSpan minimumInterval = minimumInterval ?? TimeSpan.FromSeconds(1);
    private DateTimeOffset? nextAllowedAtUtc;

    public static MusicBrainzRequestLimiter Shared { get; } = new(
        SystemMusicBrainzClock.Instance,
        TaskMusicBrainzDelay.Instance);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = clock.UtcNow;
            if (nextAllowedAtUtc.HasValue && now < nextAllowedAtUtc.Value)
            {
                await delay.DelayAsync(nextAllowedAtUtc.Value - now, cancellationToken).ConfigureAwait(false);
                now = clock.UtcNow;
            }

            nextAllowedAtUtc = now.Add(minimumInterval);
        }
        finally
        {
            semaphore.Release();
        }
    }
}

public interface IMusicBrainzResponseCache
{
    bool TryGet(string key, DateTimeOffset nowUtc, out string? json);

    void Set(string key, string json, DateTimeOffset expiresAtUtc);
}

public sealed class MusicBrainzMemoryCache : IMusicBrainzResponseCache
{
    private readonly Dictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public bool TryGet(string key, DateTimeOffset nowUtc, out string? json)
    {
        lock (gate)
        {
            if (entries.TryGetValue(key, out var entry) && entry.ExpiresAtUtc > nowUtc)
            {
                json = entry.Json;
                return true;
            }

            entries.Remove(key);
            json = null;
            return false;
        }
    }

    public void Set(string key, string json, DateTimeOffset expiresAtUtc)
    {
        lock (gate)
            entries[key] = new CacheEntry(json, expiresAtUtc);
    }

    private sealed record CacheEntry(string Json, DateTimeOffset ExpiresAtUtc);
}

public interface IMusicBrainzClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemMusicBrainzClock : IMusicBrainzClock
{
    public static SystemMusicBrainzClock Instance { get; } = new();

    private SystemMusicBrainzClock()
    {
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface IMusicBrainzDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class TaskMusicBrainzDelay : IMusicBrainzDelay
{
    public static TaskMusicBrainzDelay Instance { get; } = new();

    private TaskMusicBrainzDelay()
    {
    }

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        => Task.Delay(delay, cancellationToken);
}
