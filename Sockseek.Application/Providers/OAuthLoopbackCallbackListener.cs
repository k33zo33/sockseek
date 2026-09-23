using System.Net;
using System.Net.Sockets;
using System.Text;
using Sockseek.Integrations.Abstractions;

namespace Sockseek.Application.Providers;

public sealed class OAuthLoopbackCallbackListener : IAsyncDisposable
{
    private const string DefaultCallbackPath = "/oauth/callback";

    private readonly string providerId;
    private readonly string callbackPath;
    private readonly TcpListener listener;

    private OAuthLoopbackCallbackListener(string providerId, string callbackPath, TcpListener listener)
    {
        this.providerId = providerId;
        this.callbackPath = callbackPath;
        this.listener = listener;

        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        RedirectUri = new UriBuilder(Uri.UriSchemeHttp, IPAddress.Loopback.ToString(), endpoint.Port, callbackPath.TrimStart('/')).Uri;
    }

    public Uri RedirectUri { get; }

    public static OAuthLoopbackCallbackListener Start(string providerId, string callbackPath = DefaultCallbackPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ValidateCallbackPath(callbackPath);

        var listener = new TcpListener(IPAddress.Loopback, port: 0);
        listener.Start(backlog: 1);

        return new OAuthLoopbackCallbackListener(providerId, callbackPath, listener);
    }

    public async Task<AuthorizationCallback> WaitForCallbackAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            var result = await TryHandleClientAsync(client, cancellationToken).ConfigureAwait(false);
            if (result is not null)
                return result;
        }
    }

    public ValueTask DisposeAsync()
    {
        listener.Stop();
        return ValueTask.CompletedTask;
    }

    private async Task<AuthorizationCallback?> TryHandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(requestLine))
        {
            await WriteResponseAsync(stream, HttpStatusCode.BadRequest, "Invalid OAuth callback request.", cancellationToken).ConfigureAwait(false);
            return null;
        }

        await DrainHeadersAsync(reader, cancellationToken).ConfigureAwait(false);

        var parts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !StringComparer.Ordinal.Equals(parts[0], "GET"))
        {
            await WriteResponseAsync(stream, HttpStatusCode.MethodNotAllowed, "OAuth callback requires GET.", cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (!TryCreateRequestUri(parts[1], out var requestUri))
        {
            await WriteResponseAsync(stream, HttpStatusCode.BadRequest, "Invalid OAuth callback URI.", cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (!StringComparer.Ordinal.Equals(requestUri.AbsolutePath, callbackPath))
        {
            await WriteResponseAsync(stream, HttpStatusCode.NotFound, "OAuth callback path not found.", cancellationToken).ConfigureAwait(false);
            return null;
        }

        var query = ParseQuery(requestUri.Query);
        query.TryGetValue("state", out var state);
        query.TryGetValue("code", out var code);
        query.TryGetValue("error", out var error);

        if (string.IsNullOrWhiteSpace(state))
        {
            await WriteResponseAsync(stream, HttpStatusCode.BadRequest, "OAuth callback is missing state.", cancellationToken).ConfigureAwait(false);
            return null;
        }

        await WriteResponseAsync(stream, HttpStatusCode.OK, "OAuth callback received. You can return to Sockseek.", cancellationToken)
            .ConfigureAwait(false);

        return new AuthorizationCallback(providerId, RedirectUri, state, code, error);
    }

    private bool TryCreateRequestUri(string requestTarget, out Uri requestUri)
    {
        if (Uri.TryCreate(requestTarget, UriKind.Absolute, out var absolute))
        {
            requestUri = absolute;
            return StringComparer.OrdinalIgnoreCase.Equals(RedirectUri.Scheme, absolute.Scheme)
                && StringComparer.OrdinalIgnoreCase.Equals(RedirectUri.Host, absolute.Host)
                && RedirectUri.Port == absolute.Port;
        }

        if (Uri.TryCreate(RedirectUri, requestTarget, out requestUri!))
            return true;

        requestUri = null!;
        return false;
    }

    private static async Task DrainHeadersAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        string? line;
        do
        {
            line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }
        while (!string.IsNullOrEmpty(line));
    }

    private static IReadOnlyDictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var trimmed = query.StartsWith('?') ? query[1..] : query;
        if (trimmed.Length == 0)
            return values;

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var name = separator >= 0 ? pair[..separator] : pair;
            var value = separator >= 0 ? pair[(separator + 1)..] : string.Empty;
            values[DecodeQueryValue(name)] = DecodeQueryValue(value);
        }

        return values;
    }

    private static string DecodeQueryValue(string value)
        => Uri.UnescapeDataString(value.Replace('+', ' '));

    private static async Task WriteResponseAsync(
        Stream stream,
        HttpStatusCode statusCode,
        string body,
        CancellationToken cancellationToken)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {(int)statusCode} {ReasonPhrase(statusCode)}\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n" +
            "\r\n");

        await stream.WriteAsync(headers, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bodyBytes, cancellationToken).ConfigureAwait(false);
    }

    private static string ReasonPhrase(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.OK => "OK",
            HttpStatusCode.BadRequest => "Bad Request",
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.MethodNotAllowed => "Method Not Allowed",
            _ => statusCode.ToString(),
        };

    private static void ValidateCallbackPath(string callbackPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackPath);

        if (!callbackPath.StartsWith('/'))
            throw new ArgumentException("OAuth callback path must start with '/'.", nameof(callbackPath));
        if (callbackPath.Contains('?') || callbackPath.Contains('#'))
            throw new ArgumentException("OAuth callback path must not contain query or fragment.", nameof(callbackPath));
    }
}
