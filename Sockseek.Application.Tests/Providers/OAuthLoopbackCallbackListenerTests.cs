using System.Net;
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
        using var client = new HttpClient();
        var callbackUri = CreateCallbackUri(listener.RedirectUri, "code=authorization-code&state=expected-state");

        var pendingCallback = listener.WaitForCallbackAsync(cancellation.Token);
        var response = await client.GetAsync(callbackUri, cancellation.Token);
        var callback = await pendingCallback;

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
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
        using var client = new HttpClient();
        var callbackUri = CreateCallbackUri(listener.RedirectUri, "error=access_denied&state=expected-state");

        var pendingCallback = listener.WaitForCallbackAsync(cancellation.Token);
        var response = await client.GetAsync(callbackUri, cancellation.Token);
        var callback = await pendingCallback;

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("expected-state", callback.State);
        Assert.IsNull(callback.Code);
        Assert.AreEqual("access_denied", callback.Error);
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_IgnoresWrongPathUntilValidCallbackArrives()
    {
        await using var listener = OAuthLoopbackCallbackListener.Start(ProviderIds.Spotify);
        using var cancellation = CreateTestCancellation();
        using var client = new HttpClient();
        var wrongPathUri = new UriBuilder(listener.RedirectUri)
        {
            Path = "/wrong-path",
            Query = "code=authorization-code&state=expected-state",
        }.Uri;
        var validCallbackUri = CreateCallbackUri(listener.RedirectUri, "code=authorization-code&state=expected-state");

        var pendingCallback = listener.WaitForCallbackAsync(cancellation.Token);
        var wrongPathResponse = await client.GetAsync(wrongPathUri, cancellation.Token);
        var validResponse = await client.GetAsync(validCallbackUri, cancellation.Token);
        var callback = await pendingCallback;

        Assert.AreEqual(HttpStatusCode.NotFound, wrongPathResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, validResponse.StatusCode);
        Assert.AreEqual("expected-state", callback.State);
        Assert.AreEqual("authorization-code", callback.Code);
    }

    [TestMethod]
    public async Task WaitForCallbackAsync_RejectsMissingStateUntilValidCallbackArrives()
    {
        await using var listener = OAuthLoopbackCallbackListener.Start(ProviderIds.Spotify);
        using var cancellation = CreateTestCancellation();
        using var client = new HttpClient();
        var missingStateUri = CreateCallbackUri(listener.RedirectUri, "code=authorization-code");
        var validCallbackUri = CreateCallbackUri(listener.RedirectUri, "code=authorization-code&state=expected-state");

        var pendingCallback = listener.WaitForCallbackAsync(cancellation.Token);
        var missingStateResponse = await client.GetAsync(missingStateUri, cancellation.Token);
        var validResponse = await client.GetAsync(validCallbackUri, cancellation.Token);
        var callback = await pendingCallback;

        Assert.AreEqual(HttpStatusCode.BadRequest, missingStateResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, validResponse.StatusCode);
        Assert.AreEqual("expected-state", callback.State);
    }

    private static Uri CreateCallbackUri(Uri redirectUri, string query)
        => new UriBuilder(redirectUri)
        {
            Query = query,
        }.Uri;

    private static CancellationTokenSource CreateTestCancellation()
        => new(TimeSpan.FromSeconds(10));
}
