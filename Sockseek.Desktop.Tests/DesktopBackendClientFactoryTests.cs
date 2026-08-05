using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public class DesktopBackendClientFactoryTests
{
    [TestMethod]
    public void CreateHttpClient_UsesHandshakeBaseUrlAndBearerToken()
    {
        var handshake = new DesktopDaemonHandshake("http://127.0.0.1:5030", "desktop-token");

        using var httpClient = DesktopBackendClientFactory.CreateHttpClient(handshake);

        Assert.AreEqual(new Uri("http://127.0.0.1:5030/"), httpClient.BaseAddress);
        Assert.IsNotNull(httpClient.DefaultRequestHeaders.Authorization);
        Assert.AreEqual("Bearer", httpClient.DefaultRequestHeaders.Authorization.Scheme);
        Assert.AreEqual("desktop-token", httpClient.DefaultRequestHeaders.Authorization.Parameter);
    }

    [TestMethod]
    public void GetEventsHubUri_UsesCanonicalApiEventsPath()
    {
        var handshake = new DesktopDaemonHandshake("http://localhost:5030", "desktop-token");

        var uri = DesktopBackendClientFactory.GetEventsHubUri(handshake);

        Assert.AreEqual(new Uri("http://localhost:5030/api/events"), uri);
    }

    [TestMethod]
    public void CreateApiClient_AllowsVersionedApiClientConstruction()
    {
        var handshake = new DesktopDaemonHandshake("http://127.0.0.1:5030", "desktop-token");

        var client = DesktopBackendClientFactory.CreateApiClient(handshake);

        Assert.IsNotNull(client);
    }
}
