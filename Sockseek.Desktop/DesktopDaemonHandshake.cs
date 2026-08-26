using System.Text.Json;

namespace Sockseek.Desktop;

public sealed record DesktopDaemonHandshake(string BaseUrl, string SessionToken)
{
    public static bool TryParse(string payload, out DesktopDaemonHandshake? handshake)
    {
        handshake = null;
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        try
        {
            var dto = JsonSerializer.Deserialize<HandshakeDto>(payload);
            if (dto is null
                || string.IsNullOrWhiteSpace(dto.BaseUrl)
                || string.IsNullOrWhiteSpace(dto.SessionToken)
                || !Uri.TryCreate(dto.BaseUrl, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || !IsLoopbackHost(uri.Host))
            {
                return false;
            }

            handshake = new DesktopDaemonHandshake(dto.BaseUrl.TrimEnd('/'), dto.SessionToken.Trim());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsLoopbackHost(string host)
        => string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);

    private sealed record HandshakeDto(string BaseUrl, string SessionToken);
}
