using System.Reflection;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core;
using Sockseek.Core.Jobs;
using Sockseek.Core.Models;
using Sockseek.Core.Settings;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure;
using Sockseek.Infrastructure.Persistence;
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

            using var authorizedHttp = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(authorizedHttp);
            var state = await client.GetPlayerStateAsync();

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
            var client = new SockseekApiClient(authorized);
            var volumeState = await client.SetPlayerVolumeAsync(0.25);

            Assert.AreEqual(0.25, volumeState.Volume);

            var mutedState = await client.SetPlayerMutedAsync(true);

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

    [TestMethod]
    public async Task PlayCanonicalTrack_ReturnsLocalNowPlayingMetadata()
    {
        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpPort();
        var url = $"http://127.0.0.1:{port}";
        const string sessionToken = "player-metadata-token";
        var audioPath = CopyArtworkFixture(temp.Path, "Tagged.mp3");
        var seeded = await SeedTrackAsync(temp.Path, audioPath, codec: "mp3");
        var trackId = seeded.TrackId;
        var app = CreateApp(temp.Path, url, sessionToken);

        await app.StartAsync();
        try
        {
            using var authorizedHttp = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(authorizedHttp);

            var state = await client.PlayCanonicalTrackAsync(trackId);

            Assert.IsNotNull(state.NowPlaying);
            Assert.AreEqual("Tagged Title", state.NowPlaying.Title);
            Assert.AreEqual("Tagged Artist", state.NowPlaying.Artist);
            Assert.AreEqual("Tagged Album", state.NowPlaying.AlbumTitle);
            Assert.AreEqual(185000, state.NowPlaying.DurationMs);
            Assert.AreEqual("mp3", state.NowPlaying.Codec);
            var artworkPath = state.NowPlaying.ArtworkPath;
            Assert.IsFalse(string.IsNullOrWhiteSpace(artworkPath));
            Assert.IsNotNull(artworkPath);
            Assert.IsTrue(File.Exists(artworkPath), $"Expected cached artwork file to exist: {artworkPath}");
            StringAssert.StartsWith(
                NormalizePath(Path.GetFullPath(artworkPath)),
                NormalizePath(Path.Combine(temp.Path, "artwork-cache")) + "/");
            Assert.AreEqual("local_media_file", state.NowPlaying.Source);
            Assert.AreEqual(NormalizePath(audioPath), NormalizePath(state.Path));
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task PlayerState_RestoresDefaultQueueFromLocalDatabaseOnStartup()
    {
        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpPort();
        var url = $"http://127.0.0.1:{port}";
        const string sessionToken = "player-queue-restore-token";
        var audioPath = Path.Combine(temp.Path, "Queued.flac");
        await File.WriteAllTextAsync(audioPath, "not real audio");
        var seeded = await SeedTrackAsync(temp.Path, audioPath);
        await SeedDefaultQueueAsync(temp.Path, seeded.TrackId, seeded.FileId);
        var app = CreateApp(temp.Path, url, sessionToken);

        await app.StartAsync();
        try
        {
            using var authorizedHttp = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(authorizedHttp);

            var state = await client.GetPlayerStateAsync();

            Assert.AreEqual(1, state.Queue.Items.Count);
            Assert.AreEqual(0, state.Queue.CurrentIndex);
            Assert.AreEqual("All", state.Queue.RepeatMode);
            Assert.IsTrue(state.Queue.ShuffleEnabled);
            Assert.AreEqual(4242, state.Queue.ShuffleSeed);
            Assert.AreEqual(seeded.TrackId, state.Queue.Items[0].CanonicalTrackId);
            Assert.AreEqual(seeded.FileId, state.Queue.Items[0].LocalMediaFileId);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task PlayDownloadJob_ActiveIncompleteDownload_ReturnsBufferState()
    {
        using var temp = TemporaryDirectory.Create();
        var port = GetFreeTcpPort();
        var url = $"http://127.0.0.1:{port}";
        const string sessionToken = "player-progressive-token";
        var app = CreateApp(temp.Path, url, sessionToken, experimentalProgressivePlayback: true);
        var finalPath = Path.Combine(temp.Path, "Progressive.mp3");
        await File.WriteAllBytesAsync(finalPath + ".incomplete", new byte[32 * 1024]);
        var song = CreateActiveDownloadSong(finalPath);
        Register(app.Services.GetRequiredService<EngineStateStore>(), song);

        await app.StartAsync();
        try
        {
            using var authorizedHttp = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(authorizedHttp);

            var state = await client.PlayDownloadJobAsync(song.Id);

            Assert.AreEqual("Buffering", state.State);
            Assert.AreEqual(NormalizePath(finalPath + ".incomplete"), NormalizePath(state.Path));
            Assert.IsNotNull(state.Buffer);
            Assert.AreEqual("WaitingForInitialBuffer", state.Buffer.Status);
            Assert.IsFalse(state.Buffer.CanOpenMedia);
            Assert.AreEqual(32 * 1024, state.Buffer.AvailableBytes);
            Assert.AreEqual(960_000, state.Buffer.ExpectedBytes);
            Assert.AreEqual("Initial buffer threshold has not been reached.", state.Buffer.Reason);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private static WebApplication CreateApp(
        string tempPath,
        string url,
        string sessionToken,
        bool experimentalProgressivePlayback = false)
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
            ExperimentalProgressivePlayback = experimentalProgressivePlayback,
        }, url);

    private static async Task<(Guid TrackId, Guid FileId)> SeedTrackAsync(string tempPath, string audioPath, string codec = "flac")
    {
        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite($"Data Source={Path.Combine(tempPath, "sockseek.db")}")
            .Options;
        await using var context = new SockseekDbContext(options);
        await context.Database.MigrateAsync();

        var trackId = await new CanonicalTrackStore(context).UpsertAsync(new CanonicalTrackRecord(
            "Tagged Artist",
            "Tagged Title",
            "Tagged Album",
            185000,
            null,
            null,
            [],
            [
                new LocalMediaFileRecord(
                    audioPath,
                    1234,
                    new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero),
                    185000,
                    codec,
                    900,
                    48000,
                    24,
                    LocalMediaAvailability.Available),
            ]));
        var fileId = await context.LocalMediaFiles
            .Where(file => file.CanonicalTrackId == trackId)
            .Select(file => file.Id)
            .SingleAsync();
        return (trackId, fileId);
    }

    private static string CopyArtworkFixture(string tempPath, string fileName)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "LocalArtwork", "tone-artwork.mp3");
        var destination = Path.Combine(tempPath, fileName);
        File.Copy(source, destination);
        return destination;
    }

    private static async Task SeedDefaultQueueAsync(string tempPath, Guid trackId, Guid fileId)
    {
        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite($"Data Source={Path.Combine(tempPath, "sockseek.db")}")
            .Options;
        await using var context = new SockseekDbContext(options);
        await context.Database.MigrateAsync();
        await new PlaybackQueueStore(context, new SystemClock()).SaveAsync(new PlaybackQueueSaveRecord(
            PlaybackQueuePersistenceService.DefaultQueueId,
            "Main queue",
            0,
            PlaybackQueueRepeatMode.All,
            true,
            4242,
            [
                new PlaybackQueueItemRecord(
                    Guid.NewGuid(),
                    0,
                    trackId,
                    fileId,
                    null,
                    PlaybackQueueItemState.LocalFile),
            ]));
    }

    private static SongJob CreateActiveDownloadSong(string finalPath)
    {
        var response = new Soulseek.SearchResponse("peer", 1, true, 256, 0, []);
        var file = new Soulseek.File(
            1,
            @"remote\Artist\Album\Progressive.mp3",
            960_000,
            ".mp3",
            attributeList:
            [
                new Soulseek.FileAttribute(Soulseek.FileAttributeType.BitRate, 128),
                new Soulseek.FileAttribute(Soulseek.FileAttributeType.Length, 60),
            ]);
        var song = new SongJob(new SongQuery { Artist = "Artist", Title = "Progressive", Length = 60 })
        {
            Config = new DownloadSettings(),
            DownloadPath = finalPath,
            ResolvedTarget = new FileCandidate(response, file),
            FileSize = 960_000,
            BytesTransferred = 32 * 1024,
        };
        song.UpdateActivity(JobActivityPhase.Downloading);
        return song;
    }

    private static void Register(EngineStateStore store, SongJob song)
    {
        typeof(EngineStateStore)
            .GetMethod("OnJobRegistered", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(store, [song, null]);
    }

    private static string? NormalizePath(string? path)
        => path?.Replace('\\', '/');

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
