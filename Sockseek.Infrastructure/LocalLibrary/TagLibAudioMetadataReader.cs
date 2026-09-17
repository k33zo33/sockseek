using TagLibFile = TagLib.File;

namespace Sockseek.Infrastructure.LocalLibrary;

public sealed class TagLibAudioMetadataReader : ILocalAudioMetadataReader
{
    public Task<LocalAudioMetadata> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        using var file = TagLibFile.Create(path);
        var tag = file.Tag;
        var properties = file.Properties;

        return Task.FromResult(new LocalAudioMetadata(
            FirstValue(tag.Performers) ?? FirstValue(tag.AlbumArtists),
            Normalize(tag.Title),
            ToDurationMs(properties.Duration),
            null,
            null,
            FirstValue(properties.Codecs.Select(codec => codec.Description)),
            Positive(properties.AudioBitrate),
            Positive(properties.AudioSampleRate),
            Positive(properties.BitsPerSample)));
    }

    private static string? FirstValue(IEnumerable<string?> values)
        => values.Select(Normalize).FirstOrDefault(value => value != null);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? Positive(int value)
        => value > 0 ? value : null;

    private static int? ToDurationMs(TimeSpan duration)
        => duration > TimeSpan.Zero ? (int)Math.Round(duration.TotalMilliseconds) : null;
}
