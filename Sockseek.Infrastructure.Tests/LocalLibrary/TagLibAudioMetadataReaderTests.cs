using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Infrastructure.LocalLibrary;

namespace Sockseek.Infrastructure.Tests.LocalLibrary;

[TestClass]
public class TagLibAudioMetadataReaderTests
{
    [TestMethod]
    public async Task ReadAsync_WavFixture_ExtractsDurationCodecAndAudioProperties()
    {
        using var temp = TemporaryDirectory.Create();
        string path = Path.Combine(temp.Path, "fixture.wav");
        WritePcmWav(path, sampleRate: 44100, channels: 1, bitsPerSample: 16, duration: TimeSpan.FromSeconds(1));

        var metadata = await new TagLibAudioMetadataReader().ReadAsync(path);

        Assert.IsTrue(metadata.DurationMs is >= 900 and <= 1100, $"Unexpected duration: {metadata.DurationMs}");
        Assert.AreEqual(44100, metadata.SampleRate);
        Assert.AreEqual(16, metadata.BitDepth);
        Assert.IsFalse(string.IsNullOrWhiteSpace(metadata.Codec));
    }

    private static void WritePcmWav(
        string path,
        int sampleRate,
        short channels,
        short bitsPerSample,
        TimeSpan duration)
    {
        int bytesPerSample = bitsPerSample / 8;
        int sampleCount = (int)Math.Round(sampleRate * duration.TotalSeconds);
        int byteRate = sampleRate * channels * bytesPerSample;
        short blockAlign = (short)(channels * bytesPerSample);
        int dataLength = sampleCount * blockAlign;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
            => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sockseek-taglib-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
