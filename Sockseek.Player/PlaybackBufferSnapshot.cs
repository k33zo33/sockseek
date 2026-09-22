namespace Sockseek.Player;

public sealed record PlaybackBufferSnapshot(
    ProgressiveBufferStatus Status,
    bool CanOpenMedia,
    TimeSpan BufferedUntil,
    TimeSpan? SeekLimit,
    long AvailableBytes,
    long? ExpectedBytes,
    double? DownloadBytesPerSecond,
    string Reason);
