using Sockseek.Domain.Common;

namespace Sockseek.Domain.Tracks;

public sealed class LocalMediaFile
{
    public LocalMediaFile(
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
        Id = EntityId.New();
        Path = NormalizePath(path);
        Size = size;
        LastWriteUtc = lastWriteUtc;
        DurationMs = durationMs;
        Codec = Normalize(codec);
        Bitrate = bitrate;
        SampleRate = sampleRate;
        BitDepth = bitDepth;
        Availability = availability;
    }

    public EntityId Id { get; }
    public string Path { get; }
    public long Size { get; private set; }
    public DateTimeOffset LastWriteUtc { get; private set; }
    public int? DurationMs { get; private set; }
    public string? Codec { get; private set; }
    public int? Bitrate { get; private set; }
    public int? SampleRate { get; private set; }
    public int? BitDepth { get; private set; }
    public LocalMediaAvailability Availability { get; private set; }

    public void Refresh(long size, DateTimeOffset lastWriteUtc, int? durationMs, string? codec, int? bitrate, int? sampleRate, int? bitDepth)
    {
        Size = size;
        LastWriteUtc = lastWriteUtc;
        DurationMs = durationMs;
        Codec = Normalize(codec);
        Bitrate = bitrate;
        SampleRate = sampleRate;
        BitDepth = bitDepth;
        Availability = LocalMediaAvailability.Available;
    }

    public void MarkMissing()
        => Availability = LocalMediaAvailability.Missing;

    private static string NormalizePath(string value)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("path is required.", nameof(value))
            : value.Trim().Replace('\\', '/');

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
