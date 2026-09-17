namespace Sockseek.Infrastructure.LocalLibrary;

public sealed record LocalAudioMetadata(
    string? Artist,
    string? Title,
    int? DurationMs,
    string? Isrc,
    string? MusicBrainzRecordingId,
    string? Codec,
    int? Bitrate,
    int? SampleRate,
    int? BitDepth);
