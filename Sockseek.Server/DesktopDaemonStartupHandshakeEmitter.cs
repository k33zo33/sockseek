using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Sockseek.Server;

internal static class DesktopDaemonStartupHandshakeEmitter
{
    public const string HandshakePrefix = "SOCKSEEK_DAEMON_HANDSHAKE=";
    public const string EnableStdoutEnvironmentVariable = "SOCKSEEK_DESKTOP_HANDSHAKE_STDOUT";

    public static void Register(WebApplication app, TextWriter writer)
    {
        if (!IsEnabled())
            return;

        app.Lifetime.ApplicationStarted.Register(() => Emit(app, writer));
    }

    internal static bool IsEnabled()
        => string.Equals(Environment.GetEnvironmentVariable(EnableStdoutEnvironmentVariable), "1", StringComparison.Ordinal)
            || string.Equals(Environment.GetEnvironmentVariable(EnableStdoutEnvironmentVariable), "true", StringComparison.OrdinalIgnoreCase);

    internal static bool TryCreateHandshakeLine(IEnumerable<string> addresses, string sessionToken, out string? line)
    {
        line = null;
        if (string.IsNullOrWhiteSpace(sessionToken))
            return false;

        foreach (var address in addresses)
        {
            if (!TryNormalizeLoopbackHttpAddress(address, out var baseUrl))
                continue;

            line = HandshakePrefix + JsonSerializer.Serialize(new HandshakePayload(baseUrl, sessionToken.Trim()));
            return true;
        }

        return false;
    }

    private static void Emit(WebApplication app, TextWriter writer)
    {
        var sessionToken = app.Services.GetRequiredService<ServerSessionTokenProvider>().Token;
        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()?
            .Addresses;

        if (addresses is null)
            return;

        if (!TryCreateHandshakeLine(addresses, sessionToken, out var line) || string.IsNullOrWhiteSpace(line))
            return;

        writer.WriteLine(line);
        writer.Flush();
    }

    private static bool TryNormalizeLoopbackHttpAddress(string address, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(address)
            || !Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || !IsLoopbackHost(uri.Host))
        {
            return false;
        }

        normalized = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        return true;
    }

    private static bool IsLoopbackHost(string host)
        => string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "[::1]", StringComparison.OrdinalIgnoreCase);

    private sealed record HandshakePayload(string BaseUrl, string SessionToken);
}
