namespace Sockseek.Infrastructure.Persistence.Entities;

using Sockseek.Infrastructure.Persistence.Abstractions;

public sealed class LocalMediaFileEntity : IHasConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public Guid? CanonicalTrackId { get; set; }
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTimeOffset LastWriteUtc { get; set; }
    public int? DurationMs { get; set; }
    public string? Codec { get; set; }
    public int? Bitrate { get; set; }
    public int? SampleRate { get; set; }
    public int? BitDepth { get; set; }
    public int Availability { get; set; }

    public CanonicalTrackEntity? CanonicalTrack { get; set; }
}
