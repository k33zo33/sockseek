namespace Sockseek.Player;

public sealed record ProgressiveMediaSource(
    string Path,
    string? FinalPath,
    string CodecExtension,
    long? ExpectedBytes,
    int? BitrateKbps,
    TimeSpan? Duration,
    bool ProgressivePlaybackEnabled)
{
    public bool HasExpectedLength => ExpectedBytes is > 0;
}
