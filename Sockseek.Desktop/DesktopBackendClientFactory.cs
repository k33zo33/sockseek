using System.Net.Http.Headers;
using Sockseek.Api;

namespace Sockseek.Desktop;

public static class DesktopBackendClientFactory
{
    public static HttpClient CreateHttpClient(DesktopDaemonHandshake handshake, HttpMessageHandler? messageHandler = null)
    {
        ArgumentNullException.ThrowIfNull(handshake);

        var baseUri = GetNormalizedBaseUri(handshake);
        var httpClient = messageHandler is null
            ? new HttpClient()
            : new HttpClient(messageHandler, disposeHandler: false);

        httpClient.BaseAddress = baseUri;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", handshake.SessionToken);
        return httpClient;
    }

    public static SockseekApiClient CreateApiClient(DesktopDaemonHandshake handshake, HttpMessageHandler? messageHandler = null)
        => new(CreateHttpClient(handshake, messageHandler));

    public static Uri GetEventsHubUri(DesktopDaemonHandshake handshake)
    {
        ArgumentNullException.ThrowIfNull(handshake);
        return new Uri(GetNormalizedBaseUri(handshake), "api/events");
    }

    private static Uri GetNormalizedBaseUri(DesktopDaemonHandshake handshake)
    {
        var normalized = SockseekApiClient.NormalizeServerUrl(handshake.BaseUrl);
        if (!string.Equals(normalized.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || !normalized.IsLoopback)
        {
            throw new InvalidOperationException("Desktop backend connections must use a loopback HTTP daemon URL.");
        }

        return normalized;
    }
}
