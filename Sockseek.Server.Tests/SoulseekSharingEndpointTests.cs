using System.Net;
using System.Net.Sockets;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class SoulseekSharingEndpointTests
{
    [TestMethod]
    public async Task SharingEndpoints_RequireAuthAndManageExplicitRootsWithoutDeletingAudio()
    {
        using var temp = TemporaryDirectory.Create();
        string musicRoot = Path.Combine(temp.Path, "music");
        string albumRoot = Path.Combine(musicRoot, "Artist", "Album");
        string outputRoot = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(albumRoot);
        Directory.CreateDirectory(outputRoot);
        string firstTrack = Path.Combine(albumRoot, "one.mp3");
        await File.WriteAllBytesAsync(firstTrack, [1, 2, 3]);

        int port = GetFreeTcpPort();
        string url = $"http://127.0.0.1:{port}";
        const string sessionToken = "sharing-test-token";
        await using var app = ServerHost.Build([], new ServerOptions
        {
            DatabasePath = Path.Combine(temp.Path, "sockseek.db"),
            DatabaseBackupDir = Path.Combine(temp.Path, "backups"),
            Engine = new EngineSettings
            {
                MockFilesDir = musicRoot,
                MockFilesReadTags = false,
            },
            DefaultDownload = new DownloadSettings
            {
                Output = { ParentDir = outputRoot },
            },
            Profiles = ProfileCatalog.Empty,
            SessionToken = sessionToken,
        }, url);

        try
        {
            await app.StartAsync();

            using var anonymous = new HttpClient { BaseAddress = new Uri(url) };
            using var unauthorized = await anonymous.GetAsync("/api/v1/soulseek/sharing");
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);
            var initial = await client.GetSoulseekSharingAsync();
            Assert.AreEqual(0, initial.FileCount);
            Assert.AreEqual(0, initial.Roots.Count);

            await Assert.ThrowsExceptionAsync<SockseekApiRequestException>(() =>
                client.CreateSoulseekShareRootAsync(
                    new SaveSoulseekShareRootRequestDto(musicRoot, "bad/name")));

            var created = await client.CreateSoulseekShareRootAsync(
                new SaveSoulseekShareRootRequestDto(musicRoot, "Music"));
            var root = created.Roots.Single();
            Assert.AreEqual(Path.GetFullPath(musicRoot), root.Path);
            Assert.AreEqual("Music", root.VirtualName);
            Assert.IsTrue(root.Enabled);
            Assert.IsTrue(root.Included);
            Assert.AreEqual("ready", root.StatusCode);
            Assert.AreEqual(3, created.DirectoryCount);
            Assert.AreEqual(1, created.FileCount);

            string secondTrack = Path.Combine(albumRoot, "two.flac");
            await File.WriteAllBytesAsync(secondTrack, [4, 5, 6, 7]);
            Assert.AreEqual(1, (await client.GetSoulseekSharingAsync()).FileCount);
            var rebuilt = await client.RebuildSoulseekSharingAsync();
            Assert.AreEqual(2, rebuilt.FileCount);
            Assert.IsTrue(rebuilt.Revision > created.Revision);

            var disabled = await client.UpdateSoulseekShareRootAsync(
                root.Id,
                new SaveSoulseekShareRootRequestDto(musicRoot, "Music", Enabled: false));
            Assert.IsNotNull(disabled);
            Assert.AreEqual(0, disabled.FileCount);
            Assert.AreEqual("disabled", disabled.Roots.Single().StatusCode);
            Assert.IsFalse(disabled.Roots.Single().Included);

            Assert.IsNull(await client.UpdateSoulseekShareRootAsync(
                Guid.NewGuid(),
                new SaveSoulseekShareRootRequestDto(musicRoot, "Missing")));

            var enabled = await client.UpdateSoulseekShareRootAsync(
                root.Id,
                new SaveSoulseekShareRootRequestDto(musicRoot, "Music"));
            Assert.IsNotNull(enabled);
            Assert.AreEqual(2, enabled.FileCount);

            Assert.IsTrue(await client.DeleteSoulseekShareRootAsync(root.Id));
            Assert.IsFalse(await client.DeleteSoulseekShareRootAsync(root.Id));
            var removed = await client.GetSoulseekSharingAsync();
            Assert.AreEqual(0, removed.FileCount);
            Assert.AreEqual(0, removed.Roots.Count);
            Assert.IsTrue(File.Exists(firstTrack));
            Assert.IsTrue(File.Exists(secondTrack));
        }
        finally
        {
            await app.StopAsync();
        }
    }

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
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sockseek-sharing-api-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (!Directory.Exists(Path))
                return;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                    return;
                }
                catch (IOException) when (attempt < 19)
                {
                    Thread.Sleep(100);
                    SqliteConnection.ClearAllPools();
                }
                catch (UnauthorizedAccessException) when (attempt < 19)
                {
                    Thread.Sleep(100);
                    SqliteConnection.ClearAllPools();
                }
            }
        }
    }
}
