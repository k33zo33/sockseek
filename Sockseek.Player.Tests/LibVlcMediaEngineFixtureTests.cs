using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Player.Tests;

[TestClass]
public sealed class LibVlcMediaEngineFixtureTests
{
    [DataTestMethod]
    [DataRow("MP3", "tone.mp3")]
    [DataRow("FLAC", "tone.flac")]
    [DataRow("Ogg Vorbis", "tone.ogg")]
    [DataRow("Opus", "tone.opus")]
    [DataRow("WAV", "tone.wav")]
    [DataRow("AAC/M4A", "tone.m4a")]
    public async Task PlayAsync_LocalCodecFixture_StartsWithoutError(string codec, string fileName)
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("VideoLAN.LibVLC.Windows fixtures are validated on the Windows target.");

        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlayerCodec", fileName);
        Assert.IsTrue(File.Exists(path), $"Missing {codec} fixture at {path}.");

        using var engine = new LibVlcMediaEngine(["--aout=dummy"]);

        await engine.LoadAsync(path);
        await engine.PlayAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        await engine.StopAsync();
    }

    [TestMethod]
    public async Task PlayAsync_LongLocalFixture_RunsUntilStopped()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("VideoLAN.LibVLC.Windows fixtures are validated on the Windows target.");

        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlayerCodec", "tone-long.mp3");
        Assert.IsTrue(File.Exists(path), $"Missing long playback fixture at {path}.");

        using var engine = new LibVlcMediaEngine(["--aout=dummy"]);

        await engine.LoadAsync(path);
        await engine.PlayAsync();
        await Task.Delay(TimeSpan.FromSeconds(2));
        await engine.StopAsync();
    }

    [TestMethod]
    public async Task PlayAsync_GrowingMp3Fixture_StartsBeforeFinalBytesArrive()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("VideoLAN.LibVLC.Windows fixtures are validated on the Windows target.");

        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlayerCodec", "tone-long.mp3");
        Assert.IsTrue(File.Exists(sourcePath), $"Missing long playback fixture at {sourcePath}.");
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);
        var initialBytes = Math.Min(24 * 1024, sourceBytes.Length / 2);
        Assert.IsTrue(initialBytes > 0 && initialBytes < sourceBytes.Length);

        var incompletePath = Path.Combine(Path.GetTempPath(), "sockseek-growing-mp3-" + Guid.NewGuid().ToString("N") + ".mp3.incomplete");
        try
        {
            await File.WriteAllBytesAsync(incompletePath, sourceBytes[..initialBytes]);
            Assert.AreEqual(initialBytes, new FileInfo(incompletePath).Length);

            using var engine = new LibVlcMediaEngine(["--aout=dummy"]);

            await engine.LoadAsync(incompletePath);
            await engine.PlayAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(150));
            Assert.IsTrue(new FileInfo(incompletePath).Length < sourceBytes.Length);

            await using (var stream = new FileStream(incompletePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                await stream.WriteAsync(sourceBytes.AsMemory(initialBytes));
            }

            Assert.AreEqual(sourceBytes.Length, new FileInfo(incompletePath).Length);
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            await engine.StopAsync();
        }
        finally
        {
            if (File.Exists(incompletePath))
                File.Delete(incompletePath);
        }
    }
}
