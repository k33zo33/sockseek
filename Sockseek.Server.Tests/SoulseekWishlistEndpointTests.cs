using System.Net;
using System.Net.Sockets;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class SoulseekWishlistEndpointTests
{
    [TestMethod]
    public async Task WishlistEndpoints_RequireAuthValidateAndManageDisabledEntry()
    {
        using var temp = TemporaryDirectory.Create();
        string musicRoot = Path.Combine(temp.Path, "music");
        string outputRoot = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(musicRoot);
        Directory.CreateDirectory(outputRoot);

        int port = GetFreeTcpPort();
        string url = $"http://127.0.0.1:{port}";
        const string sessionToken = "wishlist-test-token";
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
                Output =
                {
                    ParentDir = outputRoot,
                },
            },
            Profiles = ProfileCatalog.Empty,
            SessionToken = sessionToken,
        }, url);

        try
        {
            await app.StartAsync();

            using var anonymous = new HttpClient { BaseAddress = new Uri(url) };
            using var unauthorized = await anonymous.GetAsync("/api/v1/soulseek/wishlist");
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            await Assert.ThrowsExceptionAsync<SockseekApiRequestException>(() =>
                client.CreateSoulseekWishlistEntryAsync(new SaveSoulseekWishlistEntryRequestDto("query", 1)));

            var created = await client.CreateSoulseekWishlistEntryAsync(
                new SaveSoulseekWishlistEntryRequestDto("  Artist Rare Track  ", 30, Enabled: false));
            Assert.AreEqual("Artist Rare Track", created.Query);
            Assert.IsFalse(created.Enabled);
            Assert.AreEqual("NeverRun", created.SearchState);
            Assert.IsNull(created.NextSearchAtUtc);

            var entries = await client.GetSoulseekWishlistAsync();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(created.Id, entries.Single().Id);

            await Assert.ThrowsExceptionAsync<SockseekApiRequestException>(() =>
                client.RunSoulseekWishlistEntryNowAsync(created.Id));

            var updated = await client.UpdateSoulseekWishlistEntryAsync(
                created.Id,
                new SaveSoulseekWishlistEntryRequestDto("Artist Rare Track Remaster", 60, Enabled: false));
            Assert.IsNotNull(updated);
            Assert.AreEqual("Artist Rare Track Remaster", updated.Query);
            Assert.AreEqual(60, updated.IntervalMinutes);
            Assert.IsFalse(updated.Enabled);

            Assert.IsNull(await client.UpdateSoulseekWishlistEntryAsync(
                Guid.NewGuid(),
                new SaveSoulseekWishlistEntryRequestDto("missing", 30, Enabled: false)));
            Assert.IsTrue(await client.DeleteSoulseekWishlistEntryAsync(created.Id));
            Assert.IsFalse(await client.DeleteSoulseekWishlistEntryAsync(created.Id));
            Assert.AreEqual(0, (await client.GetSoulseekWishlistAsync()).Count);
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
                "sockseek-wishlist-api-" + Guid.NewGuid().ToString("N"));
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
