using System.Net;
using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopAccountsViewModelTests
{
    [TestMethod]
    public async Task RefreshAsync_LoadsProviderCardsAndExternalAccounts()
    {
        var accountId = Guid.NewGuid();
        var authorizedAt = new DateTimeOffset(2026, 9, 23, 8, 0, 0, TimeSpan.Zero);
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/providers", StringComparison.Ordinal))
            {
                return new[]
                {
                    new ProviderCapabilityDto("bandcamp", "Bandcamp", true, false, false, true, ["ImportPublicUrl"]),
                    new ProviderCapabilityDto("spotify", "Spotify", true, false, true, false, ["ConnectAccount"]),
                };
            }

            return new[]
            {
                new ExternalAccountDto(accountId, "spotify", "user-1", "Alice", "Authorized", authorizedAt),
            };
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopAccountsViewModel(new SockseekApiClient(httpClient));

        Assert.IsTrue(await viewModel.RefreshAsync());

        CollectionAssert.AreEqual(new[] { "api/v1/providers", "api/v1/accounts" }, handler.Requests.ToArray());
        Assert.AreEqual(2, viewModel.ProviderCards.Count);
        Assert.AreEqual("Bandcamp", viewModel.ProviderCards[0].DisplayName);
        Assert.AreEqual(ProviderConnectionPrimaryAction.ImportPublicUrl, viewModel.ProviderCards[0].PrimaryAction);
        Assert.AreEqual(ProviderConnectionPrimaryAction.ConnectAccount, viewModel.ProviderCards[1].PrimaryAction);
        Assert.AreEqual(1, viewModel.Accounts.Count);
        Assert.AreEqual(accountId, viewModel.Accounts[0].AccountId);
        Assert.AreEqual("Authorized since 2026-09-23 08:00 UTC", viewModel.Accounts[0].StatusSummary);
        Assert.IsTrue(viewModel.Accounts[0].CanDisconnect);
        Assert.IsNull(viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task DisconnectAsync_PostsDisconnectAndRefreshesAccountStatus()
    {
        var accountId = Guid.NewGuid();
        var disconnected = false;
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (request.Method == HttpMethod.Post && path.EndsWith($"/accounts/{accountId}/disconnect", StringComparison.Ordinal))
            {
                disconnected = true;
                return new ExternalAccountDto(accountId, "spotify", "user-1", "Alice", "Disconnected", null);
            }

            return new[]
            {
                new ExternalAccountDto(
                    accountId,
                    "spotify",
                    "user-1",
                    "Alice",
                    disconnected ? "Disconnected" : "Authorized",
                    null),
            };
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopAccountsViewModel(new SockseekApiClient(httpClient));

        var result = await viewModel.DisconnectAsync(accountId);

        Assert.IsNotNull(result);
        Assert.AreEqual("Disconnected", result.Status);
        CollectionAssert.AreEqual(
            new[] { $"api/v1/accounts/{accountId}/disconnect", "api/v1/accounts" },
            handler.Requests.ToArray());
        Assert.AreEqual(1, viewModel.Accounts.Count);
        Assert.AreEqual("Disconnected", viewModel.Accounts[0].StatusSummary);
        Assert.IsFalse(viewModel.Accounts[0].CanDisconnect);
    }

    [TestMethod]
    public void AccountsSurface_BindsProviderCardsAccountsAndDisconnectCommand()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Sockseek.Desktop",
            "DesktopShellMainWindow.axaml"));
        var xaml = File.ReadAllText(xamlPath);

        StringAssert.Contains(xaml, "ItemsSource=\"{Binding Accounts.ProviderCards}\"");
        StringAssert.Contains(xaml, "ItemsSource=\"{Binding Accounts.Accounts}\"");
        StringAssert.Contains(xaml, "Accounts.DisconnectCommand");
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, object> responseFactory,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty);
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = JsonContent.Create(responseFactory(request)) });
        }
    }
}
