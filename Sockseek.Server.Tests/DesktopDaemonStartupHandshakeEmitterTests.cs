using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Core.Settings;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public class DesktopDaemonStartupHandshakeEmitterTests
{
    [TestMethod]
    public void TryCreateHandshakeLine_LoopbackAddress_ReturnsExpectedPayload()
    {
        var created = DesktopDaemonStartupHandshakeEmitter.TryCreateHandshakeLine(
            ["http://127.0.0.1:5030", "http://0.0.0.0:5030"],
            "test-token",
            out var line);

        Assert.IsTrue(created);
        Assert.IsNotNull(line);
        StringAssert.StartsWith(line, DesktopDaemonStartupHandshakeEmitter.HandshakePrefix);

        using var document = JsonDocument.Parse(line[DesktopDaemonStartupHandshakeEmitter.HandshakePrefix.Length..]);
        Assert.AreEqual("http://127.0.0.1:5030", document.RootElement.GetProperty("BaseUrl").GetString());
        Assert.AreEqual("test-token", document.RootElement.GetProperty("SessionToken").GetString());
    }

    [DataTestMethod]
    [DataRow("http://0.0.0.0:5030")]
    [DataRow("https://127.0.0.1:5030")]
    [DataRow("http://192.168.1.10:5030")]
    public void TryCreateHandshakeLine_NonLoopbackOrNonHttpAddress_ReturnsFalse(string address)
    {
        var created = DesktopDaemonStartupHandshakeEmitter.TryCreateHandshakeLine([address], "test-token", out var line);

        Assert.IsFalse(created);
        Assert.IsNull(line);
    }

    [TestMethod]
    public async Task ServerHostBuild_EmitsHandshakeOnStartup_WhenExplicitlyEnabled()
    {
        string? previous = Environment.GetEnvironmentVariable(DesktopDaemonStartupHandshakeEmitter.EnableStdoutEnvironmentVariable);
        Environment.SetEnvironmentVariable(DesktopDaemonStartupHandshakeEmitter.EnableStdoutEnvironmentVariable, "1");

        string musicRoot = Path.Combine(Path.GetTempPath(), "Sockseek-handshake-test-" + Guid.NewGuid());
        string outputDir = Path.Combine(Path.GetTempPath(), "Sockseek-handshake-out-" + Guid.NewGuid());
        Directory.CreateDirectory(musicRoot);
        Directory.CreateDirectory(outputDir);

        int port = GetFreeTcpPort();
        string url = $"http://127.0.0.1:{port}";
        const string sessionToken = "startup-session-token";
        var writer = new StringWriter();

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
        }, url, writer);

        try
        {
            await app.StartAsync();
            StringAssert.Contains(writer.ToString(), DesktopDaemonStartupHandshakeEmitter.HandshakePrefix);
            StringAssert.Contains(writer.ToString(), sessionToken);
            StringAssert.Contains(writer.ToString(), url);
        }
        finally
        {
            await app.StopAsync();
            Environment.SetEnvironmentVariable(DesktopDaemonStartupHandshakeEmitter.EnableStdoutEnvironmentVariable, previous);
            if (Directory.Exists(musicRoot))
                Directory.Delete(musicRoot, recursive: true);
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task ServerHostBuild_DoesNotEmitHandshake_WhenDisabled()
    {
        string? previous = Environment.GetEnvironmentVariable(DesktopDaemonStartupHandshakeEmitter.EnableStdoutEnvironmentVariable);
        Environment.SetEnvironmentVariable(DesktopDaemonStartupHandshakeEmitter.EnableStdoutEnvironmentVariable, null);

        string musicRoot = Path.Combine(Path.GetTempPath(), "Sockseek-handshake-disabled-test-" + Guid.NewGuid());
        string outputDir = Path.Combine(Path.GetTempPath(), "Sockseek-handshake-disabled-out-" + Guid.NewGuid());
        Directory.CreateDirectory(musicRoot);
        Directory.CreateDirectory(outputDir);

        int port = GetFreeTcpPort();
        string url = $"http://127.0.0.1:{port}";
        var writer = new StringWriter();

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
            SessionToken = "disabled-token",
        }, url, writer);

        try
        {
            await app.StartAsync();
            Assert.AreEqual(string.Empty, writer.ToString());
        }
        finally
        {
            await app.StopAsync();
            Environment.SetEnvironmentVariable(DesktopDaemonStartupHandshakeEmitter.EnableStdoutEnvironmentVariable, previous);
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
