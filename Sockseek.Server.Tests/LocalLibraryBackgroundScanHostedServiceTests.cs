using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class LocalLibraryBackgroundScanHostedServiceTests
{
    [TestMethod]
    public async Task BackgroundService_ScansConfiguredRootAndRescansAfterFileChange()
    {
        using var temp = TemporaryDirectory.Create();
        string libraryRoot = Path.Combine(temp.Path, "library");
        string outputDir = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(libraryRoot);
        Directory.CreateDirectory(outputDir);

        WritePcmWav(Path.Combine(libraryRoot, "First.wav"));

        var library = CreateLibraryService(temp.Path, libraryRoot, outputDir);
        await library.SaveRootAsync(new SaveLibraryRootRequestDto(libraryRoot, "Main"), CancellationToken.None);

        using var background = new LocalLibraryBackgroundScanHostedService(
            library,
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(100));

        await background.StartAsync(CancellationToken.None);
        try
        {
            await WaitForTrackCountAsync(library, 1);

            string sourcePath = Path.Combine(temp.Path, "Second.wav");
            string targetPath = Path.Combine(libraryRoot, "Second.wav");
            WritePcmWav(sourcePath);
            File.Move(sourcePath, targetPath);

            await WaitForTrackCountAsync(library, 2);
        }
        finally
        {
            await background.StopAsync(CancellationToken.None);
        }
    }

    private static LocalLibraryEndpointService CreateLibraryService(
        string basePath,
        string libraryRoot,
        string outputDir)
    {
        var options = Options.Create(new ServerOptions
        {
            DatabasePath = Path.Combine(basePath, "sockseek.db"),
            DatabaseBackupDir = Path.Combine(basePath, "backups"),
            Engine = new EngineSettings
            {
                MockFilesDir = libraryRoot,
            },
            DefaultDownload = new DownloadSettings
            {
                Output =
                {
                    ParentDir = outputDir,
                },
            },
            Profiles = ProfileCatalog.Empty,
            SessionToken = "background-library-test-token",
        });
        return new LocalLibraryEndpointService(options, new ServerDatabaseMigrationService(options));
    }

    private static async Task WaitForTrackCountAsync(
        LocalLibraryEndpointService library,
        int expectedCount)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!timeout.IsCancellationRequested)
        {
            var tracks = await library.SearchTracksAsync(null, 0, 100, includeMissing: true, CancellationToken.None);
            if (tracks.TotalCount == expectedCount)
                return;

            await Task.Delay(100, timeout.Token).ContinueWith(_ => { });
        }

        var finalTracks = await library.SearchTracksAsync(null, 0, 100, includeMissing: true, CancellationToken.None);
        Assert.Fail($"Expected {expectedCount} indexed tracks, found {finalTracks.TotalCount}.");
    }

    private static void WritePcmWav(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        const int sampleRate = 44100;
        const short channels = 1;
        const short bitsPerSample = 16;
        int sampleCount = sampleRate / 10;
        int bytesPerSample = bitsPerSample / 8;
        int dataSize = sampleCount * channels * bytesPerSample;
        int byteRate = sampleRate * channels * bytesPerSample;
        short blockAlign = (short)(channels * bytesPerSample);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
            => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sockseek-background-library-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (!Directory.Exists(Path))
                return;

            SqliteConnection.ClearAllPools();
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                    return;
                }
                catch (IOException) when (attempt < 19)
                {
                    Thread.Sleep(250);
                    SqliteConnection.ClearAllPools();
                }
                catch (UnauthorizedAccessException) when (attempt < 19)
                {
                    Thread.Sleep(250);
                    SqliteConnection.ClearAllPools();
                }
            }
        }
    }
}
