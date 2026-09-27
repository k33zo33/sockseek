using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;
using Sockseek.Infrastructure.Security;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class PlaylistEndpointTests
{
    [TestMethod]
    public async Task GetPlaylistsAndDetail_ReturnImportedPlaylistSummaryAndItems()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var playlistId = await SeedPlaylistAsync(app);
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var playlists = await client.GetPlaylistsAsync();
            var detail = await client.GetPlaylistAsync(playlistId);
            var missing = await client.GetPlaylistAsync(Guid.NewGuid());

            Assert.AreEqual(1, playlists.Count);
            Assert.IsNotNull(detail);
            Assert.IsNull(missing);
            Assert.AreEqual(playlistId, playlists.Single().PlaylistId);
            Assert.AreEqual(playlistId, detail.PlaylistId);
            Assert.AreEqual("Daily Mix", detail.Name);
            Assert.AreEqual("spotify", detail.ProviderId);
            Assert.AreEqual("playlist-1", detail.ExternalPlaylistId);
            Assert.AreEqual("Mirror", detail.ImportMode);
            Assert.AreEqual(3, detail.Resolution.TotalItems);
            Assert.AreEqual(1, detail.Resolution.AvailableLocalItems);
            Assert.AreEqual(1, detail.Resolution.UnresolvedItems);
            Assert.AreEqual(1, detail.Resolution.RemovedItems);
            Assert.AreEqual(3, detail.Items.Count);
            Assert.AreEqual("Track One", detail.Items[0].Title);
            CollectionAssert.AreEqual(new[] { "Artist One", "Guest Artist" }, detail.Items[0].Artists.ToArray());
            Assert.AreEqual("USRC17607839", detail.Items[0].Isrc);
            Assert.AreEqual("RemovedFromSourcePlaylist", detail.Items[2].Status);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetPlaylists_RequiresSessionToken()
    {
        var app = CreateApp(out var url, out _, out var tempRoot);
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(url) };

            using var response = await http.GetAsync("/api/v1/playlists");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    private static async Task<Guid> SeedPlaylistAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ServerDatabaseMigrationService>().EnsureMigratedAsync();
        var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
        var playlistId = await new ExternalPlaylistSnapshotStore(db).UpsertAsync(new ExternalPlaylistSnapshotRecord(
            ExternalProvider.Spotify,
            "playlist-1",
            "Daily Mix",
            "https://example.test/playlist/1",
            1,
            new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero),
            PlaylistImportMode.Mirror,
            "Daily Mix",
            [
                new ExternalPlaylistItemSnapshot(
                    "item-1",
                    1,
                    "Track One",
                    "Artist One",
                    "Album One",
                    180000,
                    ExternalTrackId: "track-1",
                    Isrc: "USRC17607839",
                    ExternalUrl: "https://example.test/track/1",
                    ArtworkUrl: "https://example.test/art/1.jpg",
                    MusicBrainzRecordingId: "mbid-1",
                    Artists: ["Artist One", "Guest Artist"]),
                new ExternalPlaylistItemSnapshot("item-2", 2, "Track Two", "Artist Two", "Album Two", 181000),
                new ExternalPlaylistItemSnapshot("item-3", 3, "Track Three", "Artist Three", "Album Three", 182000),
            ],
            new ExternalAccountRecord(
                ExternalProvider.Spotify,
                "user-1",
                "Alice",
                "secret://spotify/1",
                new DateTimeOffset(2026, 9, 27, 11, 55, 0, TimeSpan.Zero))));

        db.CanonicalTracks.Add(new CanonicalTrackEntity
        {
            Id = Guid.NewGuid(),
            Artist = "Artist One",
            Title = "Track One",
            AlbumTitle = "Album One",
            DurationMs = 180000,
            NormalizedArtist = "artist one",
            NormalizedTitle = "track one",
        });
        var items = await db.PlaylistItems.ToDictionaryAsync(item => item.ProviderItemId);
        items["item-1"].CanonicalTrackId = db.CanonicalTracks.Local.Single().Id;
        items["item-1"].Status = (int)PlaylistItemStatus.AvailableLocal;
        items["item-2"].Status = (int)PlaylistItemStatus.Unresolved;
        items["item-3"].Status = (int)PlaylistItemStatus.RemovedFromSourcePlaylist;
        items["item-3"].RemovedAtUtc = new DateTimeOffset(2026, 9, 27, 12, 5, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();

        return playlistId;
    }

    private static WebApplication CreateApp(out string url, out string sessionToken, out string tempRoot)
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "Sockseek-playlist-test-" + Guid.NewGuid());
        var musicRoot = Path.Combine(tempRoot, "music");
        var outputRoot = Path.Combine(tempRoot, "downloads");
        Directory.CreateDirectory(musicRoot);
        Directory.CreateDirectory(outputRoot);
        url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        sessionToken = "playlist-test-token";
        return ServerHost.Build([], new ServerOptions
        {
            DatabasePath = Path.Combine(tempRoot, "sockseek.db"),
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
            SecretStoreFactory = () => new InMemorySecretStore(),
            SessionToken = sessionToken,
        }, url);
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (!Directory.Exists(tempRoot))
            return;

        SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
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
}
