namespace Sockseek.Player;

public sealed record ProgressiveBufferPolicyOptions(
    TimeSpan InitialBufferDuration,
    long MinimumInitialBufferBytes,
    TimeSpan ResumeBufferDuration,
    long MinimumResumeBufferBytes)
{
    public static ProgressiveBufferPolicyOptions Default { get; } = new(
        InitialBufferDuration: TimeSpan.FromSeconds(10),
        MinimumInitialBufferBytes: 512 * 1024,
        ResumeBufferDuration: TimeSpan.FromSeconds(5),
        MinimumResumeBufferBytes: 256 * 1024);
}

public sealed record ProgressiveBufferSnapshot(
    long AvailableBytes,
    long? ExpectedBytes,
    double? DownloadBytesPerSecond,
    TimeSpan PlaybackPosition,
    bool DownloadCompleted,
    bool Cancelled = false);

public enum ProgressiveBufferStatus
{
    WaitingForInitialBuffer = 0,
    WaitingForComplete = 1,
    Ready = 2,
    Buffering = 3,
    Complete = 4,
    Cancelled = 5,
}

public sealed record ProgressiveBufferDecision(
    ProgressiveBufferStatus Status,
    bool CanOpenMedia,
    TimeSpan BufferedUntil,
    TimeSpan? SeekLimit,
    string Reason);

public sealed class ProgressiveBufferPolicy
{
    private readonly ProgressiveBufferPolicyOptions options;

    public ProgressiveBufferPolicy(ProgressiveBufferPolicyOptions? options = null)
        => this.options = options ?? ProgressiveBufferPolicyOptions.Default;

    public ProgressiveBufferDecision Evaluate(ProgressiveMediaSource source, ProgressiveBufferSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Path);

        if (snapshot.Cancelled)
            return Decision(ProgressiveBufferStatus.Cancelled, false, TimeSpan.Zero, null, "Download was cancelled.");

        var expectedBytes = Positive(source.ExpectedBytes) ?? Positive(snapshot.ExpectedBytes);
        var availableBytes = Math.Max(0, snapshot.AvailableBytes);
        var bufferedUntil = EstimateBufferedUntil(source, availableBytes, expectedBytes);

        if (snapshot.DownloadCompleted)
        {
            var completeUntil = source.Duration ?? bufferedUntil;
            return Decision(ProgressiveBufferStatus.Complete, true, completeUntil, completeUntil, "Download is complete.");
        }

        if (!source.ProgressivePlaybackEnabled)
            return Decision(ProgressiveBufferStatus.WaitingForComplete, false, bufferedUntil, null, "Codec is not enabled for progressive playback.");

        var initialBytes = Math.Max(
            options.MinimumInitialBufferBytes,
            BytesForDuration(source.BitrateKbps, options.InitialBufferDuration));
        if (availableBytes < initialBytes)
            return Decision(ProgressiveBufferStatus.WaitingForInitialBuffer, false, bufferedUntil, null, "Initial buffer threshold has not been reached.");

        var resumeBytes = Math.Max(
            options.MinimumResumeBufferBytes,
            BytesForDuration(source.BitrateKbps, options.ResumeBufferDuration));
        var remainingBytes = Math.Max(0, availableBytes - BytesForDuration(source.BitrateKbps, snapshot.PlaybackPosition));
        var remainingDuration = bufferedUntil - snapshot.PlaybackPosition;
        var playbackBytesPerSecond = BytesForDuration(source.BitrateKbps, TimeSpan.FromSeconds(1));
        var downloadIsSlowerThanPlayback = snapshot.DownloadBytesPerSecond is > 0
            && playbackBytesPerSecond > 0
            && snapshot.DownloadBytesPerSecond.Value < playbackBytesPerSecond;

        if (remainingDuration <= TimeSpan.Zero || remainingBytes < resumeBytes && downloadIsSlowerThanPlayback)
            return Decision(ProgressiveBufferStatus.Buffering, false, bufferedUntil, bufferedUntil, "Buffered data is below resume threshold.");

        return Decision(ProgressiveBufferStatus.Ready, true, bufferedUntil, bufferedUntil, "Progressive buffer is ready.");
    }

    private static ProgressiveBufferDecision Decision(
        ProgressiveBufferStatus status,
        bool canOpenMedia,
        TimeSpan bufferedUntil,
        TimeSpan? seekLimit,
        string reason)
        => new(status, canOpenMedia, MaxZero(bufferedUntil), seekLimit is null ? null : MaxZero(seekLimit.Value), reason);

    private static TimeSpan EstimateBufferedUntil(ProgressiveMediaSource source, long availableBytes, long? expectedBytes)
    {
        if (source.Duration is { } duration && expectedBytes is > 0)
        {
            var ratio = Math.Clamp(availableBytes / (double)expectedBytes.Value, 0, 1);
            return TimeSpan.FromTicks((long)Math.Round(duration.Ticks * ratio));
        }

        var bytesPerSecond = BytesForDuration(source.BitrateKbps, TimeSpan.FromSeconds(1));
        return bytesPerSecond > 0
            ? TimeSpan.FromSeconds(availableBytes / (double)bytesPerSecond)
            : TimeSpan.Zero;
    }

    private static long BytesForDuration(int? bitrateKbps, TimeSpan duration)
        => bitrateKbps is > 0 && duration > TimeSpan.Zero
            ? (long)Math.Ceiling(bitrateKbps.Value * 1000 / 8d * duration.TotalSeconds)
            : 0;

    private static long? Positive(long? value)
        => value is > 0 ? value : null;

    private static TimeSpan MaxZero(TimeSpan value)
        => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
