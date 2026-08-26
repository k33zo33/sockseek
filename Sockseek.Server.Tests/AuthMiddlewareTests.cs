using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public class AuthMiddlewareTests
{
    [TestMethod]
    public async Task VersionedSystemInfo_RequiresSessionToken_ButHealthAndLegacyInfoRemainAccessible()
    {
        string musicRoot = Path.Combine(Path.GetTempPath(), "Sockseek-auth-test-" + Guid.NewGuid());
        string outputDir = Path.Combine(Path.GetTempPath(), "Sockseek-auth-out-" + Guid.NewGuid());
        Directory.CreateDirectory(musicRoot);
        Directory.CreateDirectory(outputDir);

        int port = GetFreeTcpPort();
        string url = $"http://127.0.0.1:{port}";
        const string sessionToken = "test-session-token";

        await using var app = ServerHost.Build([], new ServerOptions
        {
            Engine = new EngineSettings
            {
                MockFilesDir = musicRoot,
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
            anonymous.DefaultRequestHeaders.Add(ServerHost.CorrelationIdHeaderName, "auth-test-correlation");

            using var unauthorized = await anonymous.GetAsync("/api/v1/system/info");
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            Assert.AreEqual("Bearer", unauthorized.Headers.WwwAuthenticate.Single().Scheme);
            var unauthorizedBody = await unauthorized.Content.ReadFromJsonAsync<AppErrorDto>(SockseekApiJson.CreateSerializerOptions());
            Assert.IsNotNull(unauthorizedBody);
            Assert.AreEqual("unauthorized", unauthorizedBody.Code);
            Assert.AreEqual("auth-test-correlation", unauthorizedBody.CorrelationId);

            using var anonymousHealth = await anonymous.GetAsync("/api/v1/system/health");
            anonymousHealth.EnsureSuccessStatusCode();

            using var legacyInfo = await anonymous.GetAsync("/api/server/info");
            legacyInfo.EnsureSuccessStatusCode();

            using var authorized = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var systemInfo = await authorized.GetFromJsonAsync<SystemInfoDto>("/api/v1/system/info", SockseekApiJson.CreateSerializerOptions());
            Assert.IsNotNull(systemInfo);
            Assert.AreEqual("Sockseek", systemInfo.Name);
        }
        finally
        {
            await app.StopAsync();
            if (Directory.Exists(musicRoot))
                Directory.Delete(musicRoot, recursive: true);
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
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
