using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class PlayerEndpointTests
{
    [TestMethod]
    public async Task PlayerEndpoints_RequireSessionTokenAndReturnInitialState()
    {
        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpPort();
        var url = $"http://127.0.0.1:{port}";
        const string sessionToken = "player-test-token";
        var app = CreateApp(temp.Path, url, sessionToken);

        await app.StartAsync();
        try
        {
            using var anonymous = new HttpClient { BaseAddress = new Uri(url) };
            using var anonymousResponse = await anonymous.GetAsync("/api/v1/player");
            Assert.AreEqual(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

            using var authorized = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var state = await authorized.GetFromJsonAsync<PlayerStateDto>("/api/v1/player", SockseekApiJson.CreateSerializerOptions());

            Assert.IsNotNull(state);
            Assert.AreEqual("Stopped", state.State);
            Assert.AreEqual(0, state.PositionMs);
            Assert.AreEqual(1.0, state.Volume);
            Assert.IsFalse(state.IsMuted);
            Assert.AreEqual(-1, state.Queue.CurrentIndex);
            Assert.AreEqual(0, state.Queue.Items.Count);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task PlayerVolumeAndMuteEndpoints_UpdateStateAndValidateInput()
    {
        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpPort();
        var url = $"http://127.0.0.1:{port}";
        const string sessionToken = "player-command-token";
        var app = CreateApp(temp.Path, url, sessionToken);

        await app.StartAsync();
        try
        {
            using var authorized = SockseekApiClient.CreateHttpClient(url, sessionToken);
            using var volumeResponse = await authorized.PostAsJsonAsync(
                "/api/v1/player/volume",
                new SetPlayerVolumeRequestDto(0.25),
                SockseekApiJson.CreateSerializerOptions());
            volumeResponse.EnsureSuccessStatusCode();
            var volumeState = await volumeResponse.Content.ReadFromJsonAsync<PlayerStateDto>(SockseekApiJson.CreateSerializerOptions());

            Assert.IsNotNull(volumeState);
            Assert.AreEqual(0.25, volumeState.Volume);

            using var muteResponse = await authorized.PostAsJsonAsync(
                "/api/v1/player/mute",
                new SetPlayerMutedRequestDto(true),
                SockseekApiJson.CreateSerializerOptions());
            muteResponse.EnsureSuccessStatusCode();
            var mutedState = await muteResponse.Content.ReadFromJsonAsync<PlayerStateDto>(SockseekApiJson.CreateSerializerOptions());

            Assert.IsNotNull(mutedState);
            Assert.IsTrue(mutedState.IsMuted);

            using var invalidVolumeResponse = await authorized.PostAsJsonAsync(
                "/api/v1/player/volume",
                new SetPlayerVolumeRequestDto(2),
                SockseekApiJson.CreateSerializerOptions());

            Assert.AreEqual(HttpStatusCode.BadRequest, invalidVolumeResponse.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private static WebApplication CreateApp(string tempPath, string url, string sessionToken)
        => ServerHost.Build([], new ServerOptions
        {
            DatabasePath = Path.Combine(tempPath, "sockseek.db"),
            DatabaseBackupDir = Path.Combine(tempPath, "backups"),
            Engine = new EngineSettings
            {
                MockFilesDir = tempPath,
                MockFilesReadTags = false,
            },
            DefaultDownload = new DownloadSettings
            {
                Output =
                {
                    ParentDir = tempPath,
                },
            },
            Profiles = ProfileCatalog.Empty,
            SessionToken = sessionToken,
        }, url);

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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sockseek-player-endpoint-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Path))
                DeleteWithRetry(Path);
        }

        private static void DeleteWithRetry(string path)
        {
            const int attempts = 10;
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    Directory.Delete(path, recursive: true);
                    return;
                }
                catch (IOException) when (attempt < attempts)
                {
                    Thread.Sleep(50);
                }
            }
        }
    }
}
