using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public class DesktopDaemonStartupParserTests
{
    [TestMethod]
    public void TryParseHandshakeLine_ExtractsHandshakeFromPrefixedLine()
    {
        var result = DesktopDaemonStartupParser.TryParseHandshakeLine(
            "2026-08-05 01:00:00 SOCKSEEK_DAEMON_HANDSHAKE={\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"token-1\"}",
            out var handshake);

        Assert.IsTrue(result);
        Assert.IsNotNull(handshake);
        Assert.AreEqual("http://127.0.0.1:5030", handshake.BaseUrl);
        Assert.AreEqual("token-1", handshake.SessionToken);
    }

    [TestMethod]
    public void TryParseHandshakeLine_IgnoresNonHandshakeOutput()
    {
        var result = DesktopDaemonStartupParser.TryParseHandshakeLine("regular daemon log line", out var handshake);

        Assert.IsFalse(result);
        Assert.IsNull(handshake);
    }

    [TestMethod]
    public async Task WaitForHandshakeAsync_ReturnsFirstValidHandshakeFromOutputStream()
    {
        var handshake = await DesktopDaemonStartupParser.WaitForHandshakeAsync(ReadLinesAsync(
            "booting",
            "SOCKSEEK_DAEMON_HANDSHAKE={\"BaseUrl\":\"http://localhost:5030\",\"SessionToken\":\"token-2\"}",
            "ignored-after"));

        Assert.IsNotNull(handshake);
        Assert.AreEqual("http://localhost:5030", handshake.BaseUrl);
        Assert.AreEqual("token-2", handshake.SessionToken);
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(params string[] lines)
    {
        foreach (var line in lines)
        {
            yield return line;
            await Task.Yield();
        }
    }
}
