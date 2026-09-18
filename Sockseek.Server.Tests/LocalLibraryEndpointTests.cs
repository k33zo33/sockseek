using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public class LocalLibraryEndpointTests
{
    [TestMethod]
    public async Task LibraryEndpoints_ManageRootsScanSearchDuplicatesRelinkAndDeleteRoot()
    {
        using var temp = TemporaryDirectory.Create();
        string libraryRoot = Path.Combine(temp.Path, "library");
        string relinkRoot = Path.Combine(temp.Path, "relink");
        string outputDir = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(libraryRoot);
        Directory.CreateDirectory(relinkRoot);
        Directory.CreateDirectory(outputDir);

        string firstFile = Path.Combine(libraryRoot, "A", "Local Track.wav");
        string secondFile = Path.Combine(libraryRoot, "B", "Local Track.wav");
        string relinkTarget = Path.Combine(relinkRoot, "Relink Target.wav");
        WritePcmWav(firstFile, sampleRate: 44100, channels: 1, bitsPerSample: 16, duration: TimeSpan.FromSeconds(1));
        WritePcmWav(secondFile, sampleRate: 44100, channels: 1, bitsPerSample: 16, duration: TimeSpan.FromSeconds(1));
        WritePcmWav(relinkTarget, sampleRate: 48000, channels: 1, bitsPerSample: 24, duration: TimeSpan.FromSeconds(1));

        int port = GetFreeTcpPort();
        string url = $"http://127.0.0.1:{port}";
        const string sessionToken = "library-test-token";
        await using var app = ServerHost.Build([], new ServerOptions
        {
            DatabasePath = Path.Combine(temp.Path, "sockseek.db"),
            DatabaseBackupDir = Path.Combine(temp.Path, "backups"),
            Engine = new EngineSettings
            {
                MockFilesDir = libraryRoot,
                MockFilesReadTags = false,
            },
            DefaultDownload = new DownloadSettings
            {
                Output =
                {
                    ParentDir = outputDir,
                },
            },
            Profiles = ProfileCatalog.Empty,
            SessionToken = sessionToken,
        }, url);

        try
        {
            await app.StartAsync();
            using var anonymous = new HttpClient { BaseAddress = new Uri(url) };
            using var unauthorizedResponse = await anonymous.GetAsync("/api/v1/library/roots");
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var savedRoot = await client.SaveLibraryRootAsync(new SaveLibraryRootRequestDto(libraryRoot, "Main library"));
            Assert.AreEqual("Main library", savedRoot.DisplayName);
            Assert.IsTrue(savedRoot.Enabled);
            StringAssert.EndsWith(savedRoot.Path, "/");

            var roots = await client.GetLibraryRootsAsync();
            Assert.AreEqual(1, roots.Count);
            Assert.AreEqual(savedRoot.Id, roots.Single().Id);

            var scan = await client.ScanLibraryAsync();
            Assert.AreEqual(1, scan.RootIds.Count);
            Assert.AreEqual(savedRoot.Id, scan.RootIds.Single());
            Assert.AreEqual(2, scan.ScanResult.DiscoveredFiles);
            Assert.AreEqual(2, scan.ScanResult.ImportedFiles);
            Assert.AreEqual(0, scan.ScanResult.FailedFiles);

            var search = await client.SearchLibraryTracksAsync("local track", limit: 10);
            Assert.AreEqual(1, search.TotalCount);
            var track = search.Items.Single();
            Assert.AreEqual("Unknown Artist", track.Artist);
            Assert.AreEqual("Local Track", track.Title);
            Assert.AreEqual(2, track.AvailableFileCount);
            Assert.AreEqual(0, track.MissingFileCount);
            Assert.IsNotNull(track.BestAvailableFileId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(track.BestAvailablePath));

            var duplicates = await client.GetLibraryDuplicateGroupsAsync(limit: 10);
            var duplicate = duplicates.Single();
            Assert.AreEqual(track.TrackId, duplicate.TrackId);
            Assert.AreEqual(2, duplicate.FileCount);
            Assert.AreEqual(2, duplicate.Files.Count);

            var relinkResult = await client.RelinkLocalMediaFileAsync(
                duplicate.Files.First().LocalMediaFileId,
                new RelinkLocalMediaFileRequestDto(relinkTarget));
            Assert.IsNotNull(relinkResult);
            Assert.AreEqual(track.TrackId, relinkResult.CanonicalTrackId);
            Assert.AreEqual(NormalizePath(relinkTarget), relinkResult.Path);

            var afterRelink = await client.SearchLibraryTracksAsync("local track", limit: 10);
            Assert.AreEqual(1, afterRelink.TotalCount);
            Assert.AreEqual(2, afterRelink.Items.Single().AvailableFileCount);

            Assert.IsTrue(await client.DeleteLibraryRootAsync(savedRoot.Id));
            Assert.IsFalse(await client.DeleteLibraryRootAsync(savedRoot.Id));
            Assert.AreEqual(0, (await client.GetLibraryRootsAsync()).Count);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static void WritePcmWav(
        string path,
        int sampleRate,
        short channels,
        short bitsPerSample,
        TimeSpan duration)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

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

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).Trim().Replace('\\', '/');

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
            => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sockseek-library-api-" + Guid.NewGuid().ToString("N"));
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
