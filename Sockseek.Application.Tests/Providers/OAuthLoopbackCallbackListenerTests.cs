using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Providers;
using Sockseek.Integrations.Abstractions;

namespace Tests.Application.Providers;

[TestClass]
public sealed class OAuthLoopbackCallbackListenerTests
{
    [TestMethod]
    public async Task WaitForCallbackAsync_ReturnsAuthorizationCallbackFromLoopbackGet()
    {
        await using var listener = OAuthLoopbackCallbackListener.Start(ProviderIds.Spotify);
        using var cancellation = CreateTestCancellation();
        var callbackUri = CreateCallbackUri(listener.RedirectUri, "code=authorization-code&state=expected-state");

        var pendingCallback = listener.WaitForCallbackAsync(cancellation.Token);
        var statusCode = await SendLoopbackRequestAsync(callbackUri, "GET", cancellation.Token);
        Assert.AreEqual(HttpStatusCode.OK, statusCode.StatusCode, statusCode.Body);
        var callback = await pendingCallback;
        Assert.AreEqual(ProviderIds.Spotify, callback.ProviderId);
        Assert.AreEqual(listener.RedirectUri, callback.RedirectUri);
        Assert.AreEqual("expected-state", callback.State);
        Assert.AreEqual("authorization-code", callback.Code);
        Assert.IsNull(callback.Error);
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_ReturnsProviderErrorWithoutCode()
    {
        await using var listener = OAuthLoopbackCallbackListener.Start(ProviderIds.YouTube);
        using var cancellation = CreateTestCancellation();
        var callbackUri = CreateCallbackUri(listener.RedirectUri, "error=access_denied&state=expected-state");

        var pendingCallback = listener.WaitForCallbackAsync(cancellation.Token);
        var statusCode = await SendLoopbackRequestAsync(callbackUri, "GET", cancellation.Token);
        Assert.AreEqual(HttpStatusCode.OK, statusCode.StatusCode, statusCode.Body);
        var callback = await pendingCallback;
        Assert.AreEqual("expected-state", callback.State);
        Assert.IsNull(callback.Code);
        Assert.AreEqual("access_denied", callback.Error);
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_IgnoresWrongPathUntilValidCallbackArrives()
    {
        await using var listener = OAuthLoopbackCallbackListener.Start(ProviderIds.Spotify);
        using var cancellation = CreateTestCancellation();
        var wrongPathUri = new UriBuilder(listener.RedirectUri)
        {
            Path = "/wrong-path",
            Query = "code=authorization-code&state=expected-state",
        }.Uri;
        var validCallbackUri = CreateCallbackUri(listener.RedirectUri, "code=authorization-code&state=expected-state");

        var pendingCallback = listener.WaitForCallbackAsync(cancellation.Token);
        var wrongPathStatus = await SendLoopbackRequestAsync(wrongPathUri, "GET", cancellation.Token);
        Assert.AreEqual(HttpStatusCode.NotFound, wrongPathStatus.StatusCode, wrongPathStatus.Body);
        var validStatus = await SendLoopbackRequestAsync(validCallbackUri, "GET", cancellation.Token);
        Assert.AreEqual(HttpStatusCode.OK, validStatus.StatusCode, validStatus.Body);
        var callback = await pendingCallback;
        Assert.AreEqual("expected-state", callback.State);
        Assert.AreEqual("authorization-code", callback.Code);
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_RejectsMissingStateUntilValidCallbackArrives()
    {
        await using var listener = OAuthLoopbackCallbackListener.Start(ProviderIds.Spotify);
        using var cancellation = CreateTestCancellation();
        var missingStateUri = CreateCallbackUri(listener.RedirectUri, "code=authorization-code");
        var validCallbackUri = CreateCallbackUri(listener.RedirectUri, "code=authorization-code&state=expected-state");

        var pendingCallback = listener.WaitForCallbackAsync(cancellation.Token);
        var missingStateStatus = await SendLoopbackRequestAsync(missingStateUri, "GET", cancellation.Token);
        Assert.AreEqual(HttpStatusCode.BadRequest, missingStateStatus.StatusCode, missingStateStatus.Body);
        var validStatus = await SendLoopbackRequestAsync(validCallbackUri, "GET", cancellation.Token);
        Assert.AreEqual(HttpStatusCode.OK, validStatus.StatusCode, validStatus.Body);
        var callback = await pendingCallback;
        Assert.AreEqual("expected-state", callback.State);
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_RejectsNonGetUntilValidCallbackArrives()
    {
        await using var listener = OAuthLoopbackCallbackListener.Start(ProviderIds.Spotify);
        using var cancellation = CreateTestCancellation();
        var callbackUri = CreateCallbackUri(listener.RedirectUri, "code=authorization-code&state=expected-state");

        var pendingCallback = listener.WaitForCallbackAsync(cancellation.Token);
        var postStatus = await SendLoopbackRequestAsync(callbackUri, "POST", cancellation.Token);
        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, postStatus.StatusCode, postStatus.Body);
        var validStatus = await SendLoopbackRequestAsync(callbackUri, "GET", cancellation.Token);
        Assert.AreEqual(HttpStatusCode.OK, validStatus.StatusCode, validStatus.Body);
        var callback = await pendingCallback;
        Assert.AreEqual("expected-state", callback.State);
        Assert.AreEqual("authorization-code", callback.Code);
    }

    [DataTestMethod]
    [DataRow("//evil.example/oauth/callback?code=authorization-code&state=expected-state")]
    [DataRow("file:///oauth/callback?code=authorization-code&state=expected-state")]
    public async Task WaitForCallbackAsync_RejectsNonHttpOrSchemeRelativeTargets(string requestTarget)
    {
        await using var listener = OAuthLoopbackCallbackListener.Start(ProviderIds.Spotify);
        using var cancellation = CreateTestCancellation();
        var validCallbackUri = CreateCallbackUri(listener.RedirectUri, "code=authorization-code&state=expected-state");

        var pendingCallback = listener.WaitForCallbackAsync(cancellation.Token);
        var rejected = await SendLoopbackRequestAsync(
            listener.RedirectUri,
            "GET",
            cancellation.Token,
            requestTarget);
        Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode, rejected.Body);
        var accepted = await SendLoopbackRequestAsync(validCallbackUri, "GET", cancellation.Token);
        Assert.AreEqual(HttpStatusCode.OK, accepted.StatusCode, accepted.Body);
        var callback = await pendingCallback;

        Assert.AreEqual("expected-state", callback.State);
        Assert.AreEqual("authorization-code", callback.Code);
    }

    private static Uri CreateCallbackUri(Uri redirectUri, string query)
        => new UriBuilder(redirectUri)
        {
            Query = query,
        }.Uri;

    private static async Task<(HttpStatusCode StatusCode, string Body)> SendLoopbackRequestAsync(
        Uri callbackUri,
        string method,
        CancellationToken cancellationToken,
        string? requestTarget = null)
    {
        // A raw loopback request avoids inherited proxy behavior on hosted runners.
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, callbackUri.Port, cancellationToken);
        await using var stream = client.GetStream();
        var request = Encoding.ASCII.GetBytes(
            $"{method} {requestTarget ?? callbackUri.PathAndQuery} HTTP/1.1\r\n" +
            $"Host: {callbackUri.Host}:{callbackUri.Port}\r\n" +
            "Connection: close\r\n\r\n");
        await stream.WriteAsync(request, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        using var reader = new StreamReader(stream, Encoding.ASCII);
        var statusLine = await reader.ReadLineAsync(cancellationToken);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var parts = statusLine?.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts is null || parts.Length < 2 || !int.TryParse(parts[1], out var statusCode))
            throw new InvalidOperationException($"Invalid HTTP status line: {statusLine ?? "<empty>"}");

        return ((HttpStatusCode)statusCode, body);
    }

    private static CancellationTokenSource CreateTestCancellation()
        => new(TimeSpan.FromSeconds(10));
}
