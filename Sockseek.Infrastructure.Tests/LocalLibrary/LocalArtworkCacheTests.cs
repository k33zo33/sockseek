using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Infrastructure.LocalLibrary;

namespace Sockseek.Infrastructure.Tests.LocalLibrary;

[TestClass]
public sealed class LocalArtworkCacheTests
{
    [TestMethod]
    public async Task ExtractAsync_EmbeddedPicture_WritesDeterministicCachedImage()
    {
        using var temp = TemporaryDirectory.Create();
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "LocalArtwork", "tone-artwork.mp3");
        var cacheDirectory = Path.Combine(temp.Path, "artwork-cache");
        var cache = new LocalArtworkCache(cacheDirectory);

        var first = await cache.ExtractAsync(fixture);
        var second = await cache.ExtractAsync(fixture);

        Assert.IsFalse(string.IsNullOrWhiteSpace(first));
        Assert.AreEqual(first, second);
        Assert.IsTrue(File.Exists(first), $"Expected cached artwork file to exist: {first}");
        Assert.AreEqual(".png", Path.GetExtension(first));
        StringAssert.StartsWith(
            Path.GetFullPath(first),
            Path.GetFullPath(cacheDirectory) + Path.DirectorySeparatorChar);
    }

    [TestMethod]
    public async Task ExtractAsync_FileWithoutArtwork_ReturnsNull()
    {
        using var temp = TemporaryDirectory.Create();
        var audioPath = Path.Combine(temp.Path, "tone.wav");
        WritePcmWav(audioPath, sampleRate: 44100, channels: 1, bitsPerSample: 16, duration: TimeSpan.FromMilliseconds(250));

        var result = await new LocalArtworkCache(Path.Combine(temp.Path, "artwork-cache")).ExtractAsync(audioPath);

        Assert.IsNull(result);
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sockseek-artwork-cache-" + Guid.NewGuid().ToString("N"));
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
