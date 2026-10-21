using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Security;
using Sockseek.Server;
using Tests.ClientTests;

namespace Tests.Server;

[TestClass]
public sealed class SoulseekAccountEndpointTests
{
    [TestMethod]
    public async Task AccountEndpoints_RequireAuthPersistOpaqueSecretRestartEngineAndRestoreOnRestart()
    {
        using var temp = TemporaryDirectory.Create();
        string databasePath = Path.Combine(temp.Path, "sockseek.db");
        string outputRoot = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(outputRoot);
        const string sessionToken = "soulseek-account-test-token";
        const string username = "account-test-user";
        const string password = "account-test-password";
        var secretStore = new InMemorySecretStore();
        var firstObservedCredentials = new ConcurrentQueue<(string? Username, string? Password)>();

        string firstUrl = $"http://127.0.0.1:{GetFreeTcpPort()}";
        await using (var app = CreateApp(
            firstUrl,
            databasePath,
            outputRoot,
            sessionToken,
            secretStore,
            firstObservedCredentials))
        {
            await app.StartAsync();
            await WaitForAsync(() => firstObservedCredentials.Count == 1, "Initial engine was not created.");

            using var anonymous = new HttpClient { BaseAddress = new Uri(firstUrl) };
            using var unauthorizedGet = await anonymous.GetAsync("/api/v1/soulseek/account");
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorizedGet.StatusCode);
            using var unauthorizedPut = await anonymous.PutAsJsonAsync(
                "/api/v1/soulseek/account",
                new ConfigureSoulseekAccountRequestDto(username, password));
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorizedPut.StatusCode);

            using var http = SockseekApiClient.CreateHttpClient(firstUrl, sessionToken);
            var client = new SockseekApiClient(http);
            var initial = await client.GetSoulseekAccountAsync();
            Assert.IsFalse(initial.IsConfigured);
            Assert.AreEqual("not_configured", initial.CredentialStatus);

            using var configureResponse = await http.PutAsJsonAsync(
                "api/v1/soulseek/account",
                new ConfigureSoulseekAccountRequestDto(username, password));
            Assert.AreEqual(HttpStatusCode.OK, configureResponse.StatusCode);
            string responseJson = await configureResponse.Content.ReadAsStringAsync();
            Assert.IsFalse(responseJson.Contains(password, StringComparison.Ordinal));
            Assert.IsFalse(responseJson.Contains("secret://", StringComparison.Ordinal));

            var configured = await client.GetSoulseekAccountAsync();
            Assert.AreEqual(username, configured.Username);
            Assert.IsTrue(configured.IsConfigured);
            Assert.AreEqual("configured", configured.CredentialStatus);
            await WaitForAsync(
                () => firstObservedCredentials.Count == 2,
                "Credential change did not restart the idle engine.");
            Assert.AreEqual((username, password), firstObservedCredentials.Last());

            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            var persisted = await db.SoulseekAccounts.AsNoTracking().SingleAsync();
            Assert.AreEqual(username, persisted.Username);
            StringAssert.StartsWith(persisted.SecretReference, "secret://memory/");
            Assert.IsFalse(persisted.SecretReference.Contains(password, StringComparison.Ordinal));
            var secret = await secretStore.ReadAsync(persisted.SecretReference);
            Assert.IsNotNull(secret);
            Assert.AreEqual(password, secret.Secrets["password"]);
        }

        var restoredCredentials = new ConcurrentQueue<(string? Username, string? Password)>();
        string secondUrl = $"http://127.0.0.1:{GetFreeTcpPort()}";
        await using (var restartedApp = CreateApp(
            secondUrl,
            databasePath,
            outputRoot,
            sessionToken,
            secretStore,
            restoredCredentials))
        {
            await restartedApp.StartAsync();
            await WaitForAsync(() => restoredCredentials.Count == 1, "Restarted engine was not created.");
            Assert.AreEqual((username, password), restoredCredentials.Single());

            using var http = SockseekApiClient.CreateHttpClient(secondUrl, sessionToken);
            var client = new SockseekApiClient(http);
            var restored = await client.GetSoulseekAccountAsync();
            Assert.AreEqual(username, restored.Username);
            Assert.IsTrue(restored.IsConfigured);
            Assert.AreEqual("configured", restored.CredentialStatus);

            await using var scope = restartedApp.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            string secretReference = (await db.SoulseekAccounts.AsNoTracking().SingleAsync()).SecretReference;

            var disconnected = await client.DisconnectSoulseekAccountAsync();
            Assert.IsFalse(disconnected.IsConfigured);
            Assert.IsNull(disconnected.Username);
            Assert.AreEqual("not_configured", disconnected.CredentialStatus);
            Assert.AreEqual(0, await db.SoulseekAccounts.CountAsync());
            Assert.IsNull(await secretStore.ReadAsync(secretReference));
        }
    }

    [TestMethod]
    public async Task ConfigureAccount_InvalidPasswordReturnsStructuredBadRequest()
    {
        using var temp = TemporaryDirectory.Create();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        const string sessionToken = "soulseek-account-validation-token";
        await using var app = CreateApp(
            url,
            Path.Combine(temp.Path, "sockseek.db"),
            Path.Combine(temp.Path, "output"),
            sessionToken,
            new InMemorySecretStore(),
            new ConcurrentQueue<(string? Username, string? Password)>());
        await app.StartAsync();

        using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
        var client = new SockseekApiClient(http);
        var exception = await Assert.ThrowsExceptionAsync<SockseekApiRequestException>(() =>
            client.ConfigureSoulseekAccountAsync(new ConfigureSoulseekAccountRequestDto("user", "")));

        StringAssert.Contains(exception.Message, "400");
        StringAssert.Contains(exception.Message, "invalid_soulseek_account");
    }

    private static WebApplication CreateApp(
        string url,
        string databasePath,
        string outputRoot,
        string sessionToken,
        InMemorySecretStore secretStore,
        ConcurrentQueue<(string? Username, string? Password)> observedCredentials)
    {
        Directory.CreateDirectory(outputRoot);
        return ServerHost.Build([], new ServerOptions
        {
            DatabasePath = databasePath,
            DatabaseBackupDir = Path.Combine(Path.GetDirectoryName(databasePath)!, "backups"),
            Engine = new EngineSettings(),
            DefaultDownload = new DownloadSettings { Output = { ParentDir = outputRoot } },
            Profiles = ProfileCatalog.Empty,
            SessionToken = sessionToken,
            SecretStoreFactory = () => secretStore,
            ClientFactory = settings =>
            {
                observedCredentials.Enqueue((settings.Username, settings.Password));
                return new MockSoulseekClient([], initialState: Soulseek.SoulseekClientStates.None);
            },
        }, url);
    }

    private static async Task WaitForAsync(Func<bool> condition, string failureMessage)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested)
        {
            if (condition())
                return;

            try
            {
                await Task.Delay(25, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Assert.Fail(failureMessage);
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
                "sockseek-account-api-" + Guid.NewGuid().ToString("N"));
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
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException
                    && attempt < 19)
                {
                    Thread.Sleep(100);
                    SqliteConnection.ClearAllPools();
                }
            }
        }
    }
}
