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
}
