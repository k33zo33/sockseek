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
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class ProviderEndpointTests
{
    [TestMethod]
    public async Task GetProviders_ReturnsCapabilityDrivenProviderList()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var providers = await client.GetProvidersAsync();
            var bandcamp = providers.Single(provider => provider.ProviderId == "bandcamp");
            var spotify = providers.Single(provider => provider.ProviderId == "spotify");
            var musicBrainz = providers.Single(provider => provider.ProviderId == "musicbrainz");

            Assert.IsTrue(spotify.SupportsAccountConnection);
            Assert.IsTrue(spotify.Capabilities.Contains("ConnectAccount"));
            Assert.IsTrue(bandcamp.SupportsPublicUrlImport);
            Assert.IsFalse(bandcamp.SupportsAccountConnection);
            CollectionAssert.DoesNotContain(bandcamp.Capabilities.ToArray(), "ConnectAccount");
            Assert.IsTrue(musicBrainz.SupportsMetadataLookup);
            Assert.IsFalse(musicBrainz.SupportsPlaylistImport);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetProviderCapabilities_ReturnsSingleProviderOrNotFound()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var provider = await client.GetProviderCapabilitiesAsync("youtube");
            var missing = await client.GetProviderCapabilitiesAsync("missing-provider");

            Assert.IsNotNull(provider);
            Assert.AreEqual("youtube", provider.ProviderId);
            Assert.IsTrue(provider.SupportsAccountConnection);
            Assert.IsNull(missing);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetProviders_RequiresSessionToken()
    {
        var app = CreateApp(out var url, out _, out var tempRoot);
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(url) };

            using var response = await http.GetAsync("/api/v1/providers");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetExternalAccounts_ReturnsAccountStatusWithoutSecretReference()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var accountId = await SeedExternalAccountAsync(app);
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var accounts = await client.GetExternalAccountsAsync();
            var rawJson = await http.GetStringAsync("api/v1/accounts");

            var account = accounts.Single();
            Assert.AreEqual(accountId, account.AccountId);
            Assert.AreEqual("spotify", account.ProviderId);
            Assert.AreEqual("user-1", account.ExternalUserId);
            Assert.AreEqual("Alice", account.DisplayName);
            Assert.AreEqual("Authorized", account.Status);
            Assert.IsFalse(rawJson.Contains("secretReference", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(rawJson.Contains("secret://", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(rawJson.Contains("access-token", StringComparison.Ordinal));
            Assert.IsFalse(rawJson.Contains("refresh-token", StringComparison.Ordinal));
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task DisconnectExternalAccount_ClearsSecretReferenceAndReturnsDisconnectedStatus()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var accountId = await SeedExternalAccountAsync(app);
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var disconnected = await client.DisconnectExternalAccountAsync(accountId);

            Assert.IsNotNull(disconnected);
            Assert.AreEqual(accountId, disconnected.AccountId);
            Assert.AreEqual("Disconnected", disconnected.Status);
            await using var verifyScope = app.Services.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            var account = await verifyDb.ExternalAccounts.SingleAsync(entity => entity.Id == accountId);
            Assert.AreEqual((int)ExternalAccountStatus.Disconnected, account.Status);
            Assert.AreEqual(string.Empty, account.SecretReference);
            Assert.AreEqual(1, await verifyDb.ExternalPlaylists.CountAsync(playlist => playlist.AccountId == accountId));
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task DisconnectExternalAccount_ReturnsNotFoundForMissingAccount()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var disconnected = await client.DisconnectExternalAccountAsync(Guid.NewGuid());

            Assert.IsNull(disconnected);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetExternalAccounts_RequiresSessionToken()
    {
        var app = CreateApp(out var url, out _, out var tempRoot);
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(url) };

            using var response = await http.GetAsync("/api/v1/accounts");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    private static async Task<Guid> SeedExternalAccountAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ServerDatabaseMigrationService>().EnsureMigratedAsync();
        var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
        var secretReference = "secret://windows-dpapi/" + Guid.NewGuid().ToString("N");

        var account = new ExternalAccountEntity
        {
            Id = Guid.NewGuid(),
            Provider = (int)ExternalProvider.Spotify,
            ExternalUserId = "user-1",
            DisplayName = "Alice",
            SecretReference = secretReference,
            Status = (int)ExternalAccountStatus.Authorized,
            LastAuthorizedAtUtc = new DateTimeOffset(2026, 9, 23, 8, 0, 0, TimeSpan.Zero),
        };
        var playlist = new ExternalPlaylistEntity
        {
            Id = Guid.NewGuid(),
            Account = account,
            Provider = (int)ExternalProvider.Spotify,
            ExternalId = "playlist-1",
            Name = "Daily Mix",
            SnapshotVersion = 1,
            LastSyncedAtUtc = new DateTimeOffset(2026, 9, 23, 8, 5, 0, TimeSpan.Zero),
        };

        db.AddRange(account, playlist);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static WebApplication CreateApp(out string url, out string sessionToken, out string tempRoot)
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "Sockseek-provider-test-" + Guid.NewGuid());
        var musicRoot = Path.Combine(tempRoot, "music");
        var outputRoot = Path.Combine(tempRoot, "downloads");
        Directory.CreateDirectory(musicRoot);
        Directory.CreateDirectory(outputRoot);
        url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        sessionToken = "provider-test-token";
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
