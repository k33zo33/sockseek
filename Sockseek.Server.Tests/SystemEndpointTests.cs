using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public class SystemEndpointTests
{
    [TestMethod]
    public async Task Health_And_SystemInfo_ReturnExpectedFoundationMetadata()
    {
        string musicRoot = Path.Combine(Path.GetTempPath(), "Sockseek-system-test-" + Guid.NewGuid());
        string outputDir = Path.Combine(Path.GetTempPath(), "Sockseek-system-out-" + Guid.NewGuid());
        Directory.CreateDirectory(musicRoot);
        Directory.CreateDirectory(outputDir);

        int port = GetFreeTcpPort();
        string url = $"http://127.0.0.1:{port}";
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
        }, url);

        try
        {
            await app.StartAsync();
            using var http = new HttpClient { BaseAddress = new Uri(url) };
            http.DefaultRequestHeaders.Add(ServerHost.CorrelationIdHeaderName, "system-test-correlation");

            var healthResponse = await http.GetAsync("/health");
            healthResponse.EnsureSuccessStatusCode();
            Assert.AreEqual("system-test-correlation", healthResponse.Headers.GetValues(ServerHost.CorrelationIdHeaderName).Single());

            var health = await healthResponse.Content.ReadFromJsonAsync<SystemHealthDto>(SockseekApiJson.CreateSerializerOptions());
            Assert.IsNotNull(health);
            Assert.AreEqual("ok", health.Status);
            Assert.AreEqual("system-test-correlation", health.CorrelationId);

            var systemInfo = await http.GetFromJsonAsync<SystemInfoDto>("/api/v1/system/info", SockseekApiJson.CreateSerializerOptions());
            Assert.IsNotNull(systemInfo);
            Assert.AreEqual("Sockseek", systemInfo.Name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(systemInfo.Version));
            Assert.IsFalse(string.IsNullOrWhiteSpace(systemInfo.Commit));
            Assert.IsTrue(systemInfo.Capabilities.LegacyApi);
            Assert.IsTrue(systemInfo.Capabilities.VersionedApi);
            Assert.IsTrue(systemInfo.Capabilities.SignalR);
            Assert.IsTrue(systemInfo.Capabilities.StructuredErrors);
            Assert.IsTrue(systemInfo.Capabilities.CorrelationIds);

            var versionedHealthResponse = await http.GetAsync("/api/v1/system/health");
            versionedHealthResponse.EnsureSuccessStatusCode();
            var versionedHealth = await versionedHealthResponse.Content.ReadFromJsonAsync<SystemHealthDto>(SockseekApiJson.CreateSerializerOptions());
            Assert.IsNotNull(versionedHealth);
            Assert.AreEqual("system-test-correlation", versionedHealth.CorrelationId);
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

    [TestMethod]
    public async Task VersionedApi_UnhandledExceptions_ReturnStructuredErrorEnvelope()
    {
        string musicRoot = Path.Combine(Path.GetTempPath(), "Sockseek-system-error-test-" + Guid.NewGuid());
        string outputDir = Path.Combine(Path.GetTempPath(), "Sockseek-system-error-out-" + Guid.NewGuid());
        Directory.CreateDirectory(musicRoot);
        Directory.CreateDirectory(outputDir);

        int port = GetFreeTcpPort();
        string url = $"http://127.0.0.1:{port}";
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
        }, url);
        app.MapGet("/api/v1/system/test-error", (HttpContext _) => throw new InvalidOperationException("boom"));

        try
        {
            await app.StartAsync();
            using var http = new HttpClient { BaseAddress = new Uri(url) };
            http.DefaultRequestHeaders.Add(ServerHost.CorrelationIdHeaderName, "error-test-correlation");

            using var response = await http.GetAsync("/api/v1/system/test-error");
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.AreEqual("error-test-correlation", response.Headers.GetValues(ServerHost.CorrelationIdHeaderName).Single());

            var error = await response.Content.ReadFromJsonAsync<AppErrorDto>(SockseekApiJson.CreateSerializerOptions());
            Assert.IsNotNull(error);
            Assert.AreEqual("internal_error", error.Code);
            Assert.AreEqual("error-test-correlation", error.CorrelationId);
            StringAssert.Contains(error.Message, "unexpected");
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
