using Sockseek.Domain.Accounts;
using Sockseek.Domain.Common;

namespace Sockseek.Domain.Tracks;

public sealed class CanonicalTrack
{
    private readonly List<TrackSource> sources = [];
    private readonly List<LocalMediaFile> localMediaFiles = [];

    public CanonicalTrack(string artist, string title, int? durationMs, string? isrc = null, string? musicBrainzRecordingId = null)
    {
        Id = EntityId.New();
        Artist = Require(artist, nameof(artist));
        Title = Require(title, nameof(title));
        NormalizedArtist = NormalizeForMatch(Artist);
        NormalizedTitle = NormalizeForMatch(Title);
        DurationMs = durationMs;
        Isrc = NormalizeCode(isrc);
        MusicBrainzRecordingId = NormalizeCode(musicBrainzRecordingId);
    }

    public EntityId Id { get; }
    public string Artist { get; }
    public string Title { get; }
    public int? DurationMs { get; }
    public string? Isrc { get; }
    public string? MusicBrainzRecordingId { get; }
    public string NormalizedArtist { get; }
    public string NormalizedTitle { get; }
    public IReadOnlyList<TrackSource> Sources => sources;
    public IReadOnlyList<LocalMediaFile> LocalMediaFiles => localMediaFiles;

    public TrackSource AddSource(ExternalProvider provider, string externalId, string? externalUrl, string? rawMetadataJson)
    {
        if (sources.Any(source => source.Provider == provider && string.Equals(source.ExternalId, externalId, StringComparison.Ordinal)))
            throw new InvalidOperationException("Track source already exists for this provider and external id.");

        var source = new TrackSource(provider, externalId, externalUrl, rawMetadataJson);
        sources.Add(source);
        return source;
    }

    public LocalMediaFile AddLocalMediaFile(
        string path,
        long size,
        DateTimeOffset lastWriteUtc,
        int? durationMs,
        string? codec,
        int? bitrate,
        int? sampleRate,
        int? bitDepth,
        LocalMediaAvailability availability)
    {
        string normalizedPath = NormalizePath(path);
        if (localMediaFiles.Any(file => string.Equals(file.Path, normalizedPath, StringComparison.Ordinal)))
            throw new InvalidOperationException("Local media file already exists for this path.");

        var file = new LocalMediaFile(normalizedPath, size, lastWriteUtc, durationMs, codec, bitrate, sampleRate, bitDepth, availability);
        localMediaFiles.Add(file);
        return file;
    }

    internal static string NormalizeForMatch(string value)
    {
        var chars = Require(value, nameof(value))
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();

        return string.Join(' ', new string(chars)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CanonicalizeToken));
    }

    private static string CanonicalizeToken(string token)
        => token switch
        {
            "ft" => "feat",
            "featuring" => "feat",
            _ => token,
        };

    private static string Require(string value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} is required.", paramName)
            : value.Trim();

    private static string? NormalizeCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string NormalizePath(string value)
        => Require(value, nameof(value)).Replace('\\', '/');
}
