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
            if (path.EndsWith("/soulseek/account", StringComparison.Ordinal))
            {
                return new SoulseekAccountDto(
                    "listener",
                    IsConfigured: true,
                    IsConnected: false,
                    "Connected",
                    "configured",
                    authorizedAt);
            }
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

        CollectionAssert.AreEqual(
            new[] { "api/v1/soulseek/account", "api/v1/providers", "api/v1/accounts" },
            handler.Requests.ToArray());
        Assert.AreEqual("listener", viewModel.SoulseekUsername);
        Assert.AreEqual("Configured as listener - Connected", viewModel.SoulseekStatusSummary);
        Assert.IsTrue(viewModel.CanDisconnectSoulseekAccount);
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
    public async Task SaveSoulseekAccountAsync_PutsCredentialsAndClearsPasswordAfterSuccess()
    {
        ConfigureSoulseekAccountRequestDto? captured = null;
        var handler = new AsyncRecordingHandler(async request =>
        {
            captured = await request.Content!.ReadFromJsonAsync<ConfigureSoulseekAccountRequestDto>();
            return new SoulseekAccountDto(
                captured!.Username,
                IsConfigured: true,
                IsConnected: false,
                "Connecting",
                "configured",
                DateTimeOffset.UtcNow);
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopAccountsViewModel(new SockseekApiClient(httpClient))
        {
            SoulseekUsername = "listener",
            SoulseekPassword = "local-password",
        };

        var result = await viewModel.SaveSoulseekAccountAsync();

        Assert.IsNotNull(result);
        Assert.IsNotNull(captured);
        Assert.AreEqual("listener", captured.Username);
        Assert.AreEqual("local-password", captured.Password);
        Assert.AreEqual(string.Empty, viewModel.SoulseekPassword);
        Assert.AreEqual("Configured as listener - Connecting", viewModel.SoulseekStatusSummary);
        Assert.IsFalse(viewModel.CanSaveSoulseekAccount);
        CollectionAssert.AreEqual(new[] { "api/v1/soulseek/account" }, handler.Requests.ToArray());
    }

    [TestMethod]
    public async Task DisconnectSoulseekAccountAsync_DeletesCredentialsAndClearsUsername()
    {
        var handler = new RecordingHandler(_ => new SoulseekAccountDto(
            null,
            IsConfigured: false,
            IsConnected: false,
            "None",
            "not_configured",
            null));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopAccountsViewModel(new SockseekApiClient(httpClient))
        {
            SoulseekUsername = "listener",
            SoulseekPassword = "discard-me",
        };

        var result = await viewModel.DisconnectSoulseekAccountAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual(string.Empty, viewModel.SoulseekUsername);
        Assert.AreEqual(string.Empty, viewModel.SoulseekPassword);
        Assert.AreEqual("Not configured", viewModel.SoulseekStatusSummary);
        CollectionAssert.AreEqual(new[] { "api/v1/soulseek/account" }, handler.Requests.ToArray());
    }

    [TestMethod]
    public async Task RefreshAsync_MissingStoredSecretStillAllowsDisconnectRecovery()
    {
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/soulseek/account", StringComparison.Ordinal))
            {
                return new SoulseekAccountDto(
                    "listener",
                    IsConfigured: false,
                    IsConnected: false,
                    "None",
                    "secret_unavailable",
                    DateTimeOffset.UtcNow);
            }
            if (path.EndsWith("/providers", StringComparison.Ordinal))
                return Array.Empty<ProviderCapabilityDto>();
            return Array.Empty<ExternalAccountDto>();
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopAccountsViewModel(new SockseekApiClient(httpClient));

        Assert.IsTrue(await viewModel.RefreshAsync());

        Assert.AreEqual("listener needs credentials again", viewModel.SoulseekStatusSummary);
        Assert.IsTrue(viewModel.CanDisconnectSoulseekAccount);
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
        StringAssert.Contains(xaml, "Accounts.SaveSoulseekAccountCommand");
        StringAssert.Contains(xaml, "Accounts.DisconnectSoulseekAccountCommand");
        StringAssert.Contains(xaml, "Accounts.SoulseekUsername, Mode=TwoWay");
        StringAssert.Contains(xaml, "Accounts.SoulseekPassword, Mode=TwoWay");
        StringAssert.Contains(xaml, "PasswordChar=\"*\"");
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

    private sealed class AsyncRecordingHandler(
        Func<HttpRequestMessage, Task<object>> responseFactory) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(await responseFactory(request)),
            };
        }
    }
}
