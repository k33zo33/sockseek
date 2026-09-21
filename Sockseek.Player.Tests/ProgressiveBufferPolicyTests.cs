using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Player.Tests;

[TestClass]
public sealed class ProgressiveBufferPolicyTests
{
    private static readonly ProgressiveBufferPolicyOptions TestOptions = new(
        InitialBufferDuration: TimeSpan.FromSeconds(5),
        MinimumInitialBufferBytes: 64 * 1024,
        ResumeBufferDuration: TimeSpan.FromSeconds(3),
        MinimumResumeBufferBytes: 32 * 1024);

    [TestMethod]
    public void Evaluate_ProgressiveMp3HasInitialBuffer_ReturnsReadyBeforeComplete()
    {
        var policy = new ProgressiveBufferPolicy(TestOptions);
        var source = CreateSource(progressiveEnabled: true);

        var result = policy.Evaluate(source, new ProgressiveBufferSnapshot(
            AvailableBytes: 320_000,
            ExpectedBytes: 960_000,
            DownloadBytesPerSecond: 96_000,
            PlaybackPosition: TimeSpan.Zero,
            DownloadCompleted: false));

        Assert.AreEqual(ProgressiveBufferStatus.Ready, result.Status);
        Assert.IsTrue(result.CanOpenMedia);
        Assert.AreEqual(TimeSpan.FromSeconds(20), result.BufferedUntil);
        Assert.AreEqual(result.BufferedUntil, result.SeekLimit);
    }

    [TestMethod]
    public void Evaluate_UnsupportedCodecWaitsUntilComplete()
    {
        var policy = new ProgressiveBufferPolicy(TestOptions);
        var source = CreateSource(progressiveEnabled: false);

        var waiting = policy.Evaluate(source, new ProgressiveBufferSnapshot(
            AvailableBytes: 900_000,
            ExpectedBytes: 960_000,
            DownloadBytesPerSecond: 128_000,
            PlaybackPosition: TimeSpan.Zero,
            DownloadCompleted: false));
        var complete = policy.Evaluate(source, new ProgressiveBufferSnapshot(
            AvailableBytes: 960_000,
            ExpectedBytes: 960_000,
            DownloadBytesPerSecond: null,
            PlaybackPosition: TimeSpan.Zero,
            DownloadCompleted: true));

        Assert.AreEqual(ProgressiveBufferStatus.WaitingForComplete, waiting.Status);
        Assert.IsFalse(waiting.CanOpenMedia);
        Assert.AreEqual(ProgressiveBufferStatus.Complete, complete.Status);
        Assert.IsTrue(complete.CanOpenMedia);
    }

    [TestMethod]
    public void Evaluate_SlowDownloadNearBufferedEnd_EntersBuffering()
    {
        var policy = new ProgressiveBufferPolicy(TestOptions);
        var source = CreateSource(progressiveEnabled: true);

        var result = policy.Evaluate(source, new ProgressiveBufferSnapshot(
            AvailableBytes: 192_000,
            ExpectedBytes: 960_000,
            DownloadBytesPerSecond: 8_000,
            PlaybackPosition: TimeSpan.FromSeconds(11),
            DownloadCompleted: false));

        Assert.AreEqual(ProgressiveBufferStatus.Buffering, result.Status);
        Assert.IsFalse(result.CanOpenMedia);
        Assert.AreEqual(TimeSpan.FromSeconds(12), result.BufferedUntil);
        Assert.AreEqual(result.BufferedUntil, result.SeekLimit);
    }

    [TestMethod]
    public void Evaluate_BufferGrowthAfterUnderrun_ReturnsReady()
    {
        var policy = new ProgressiveBufferPolicy(TestOptions);
        var source = CreateSource(progressiveEnabled: true);

        var result = policy.Evaluate(source, new ProgressiveBufferSnapshot(
            AvailableBytes: 512_000,
            ExpectedBytes: 960_000,
            DownloadBytesPerSecond: 96_000,
            PlaybackPosition: TimeSpan.FromSeconds(11),
            DownloadCompleted: false));

        Assert.AreEqual(ProgressiveBufferStatus.Ready, result.Status);
        Assert.IsTrue(result.CanOpenMedia);
        Assert.AreEqual(TimeSpan.FromSeconds(32), result.BufferedUntil);
    }

    [TestMethod]
    public void Evaluate_CancelledDownload_ReturnsCancelledAndCannotOpen()
    {
        var policy = new ProgressiveBufferPolicy(TestOptions);
        var source = CreateSource(progressiveEnabled: true);

        var result = policy.Evaluate(source, new ProgressiveBufferSnapshot(
            AvailableBytes: 512_000,
            ExpectedBytes: 960_000,
            DownloadBytesPerSecond: 96_000,
            PlaybackPosition: TimeSpan.Zero,
            DownloadCompleted: false,
            Cancelled: true));

        Assert.AreEqual(ProgressiveBufferStatus.Cancelled, result.Status);
        Assert.IsFalse(result.CanOpenMedia);
        Assert.IsNull(result.SeekLimit);
    }

    private static ProgressiveMediaSource CreateSource(bool progressiveEnabled)
        => new(
            Path: "C:/Music/Track.mp3.incomplete",
            FinalPath: "C:/Music/Track.mp3",
            CodecExtension: ".mp3",
            ExpectedBytes: 960_000,
            BitrateKbps: 128,
            Duration: TimeSpan.FromSeconds(60),
            ProgressivePlaybackEnabled: progressiveEnabled);
}
