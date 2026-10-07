using System.Net;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Desktop;

namespace Tests.Desktop;

[TestClass]
public sealed class DesktopAboutViewModelTests
{
    [TestMethod]
    public async Task RefreshAsync_LoadsReleaseMetadataFromSystemInfo()
    {
        var handler = new SystemInfoHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopAboutViewModel(new SockseekApiClient(httpClient));

        Assert.IsTrue(await viewModel.RefreshAsync());

        Assert.AreEqual("Sockseek", viewModel.ProductName);
        Assert.AreEqual("3.0.5", viewModel.Version);
        Assert.AreEqual("abc123", viewModel.Commit);
        Assert.AreEqual("https://github.com/k33zo33/sockseek", viewModel.SourceUrl);
        Assert.AreEqual("AGPL-3.0", viewModel.License);
        StringAssert.Contains(viewModel.VersionSummary, "3.0.5");
        StringAssert.Contains(viewModel.VersionSummary, "abc123");
        StringAssert.Contains(viewModel.LicenseSummary, "GNU AGPL-3.0");
        StringAssert.Contains(viewModel.ReleaseStatusSummary, "closed/internal testing");
        StringAssert.Contains(viewModel.LegalUseSummary, "legally allowed");
        StringAssert.Contains(viewModel.LegalUseSummary, "metadata/import sources only");
        Assert.AreEqual(string.Empty, viewModel.ErrorMessage);
        Assert.AreEqual("/api/v1/system/info", handler.RequestPath);
    }

    [TestMethod]
    public void ShellSurface_BindsAboutAndLicenseFields()
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

        StringAssert.Contains(xaml, "About and license");
        StringAssert.Contains(xaml, "About.RefreshCommand");
        StringAssert.Contains(xaml, "About.VersionSummary");
        StringAssert.Contains(xaml, "About.LicenseSummary");
        StringAssert.Contains(xaml, "About.ReleaseStatusSummary");
        StringAssert.Contains(xaml, "About.LegalUseSummary");
        StringAssert.Contains(xaml, "About.SourceUrl");
        StringAssert.Contains(xaml, "About.Commit");
    }

    private sealed class SystemInfoHandler : HttpMessageHandler
    {
        public string? RequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri?.AbsolutePath;
            var info = new SystemInfoDto(
                "Sockseek",
                "3.0.5",
                "abc123",
                "https://github.com/k33zo33/sockseek",
                "AGPL-3.0",
                new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero),
                new SystemCapabilitiesDto(
                    LegacyApi: true,
                    VersionedApi: true,
                    SignalR: true,
                    StructuredErrors: true,
                    CorrelationIds: true));
            var json = JsonSerializer.Serialize(info, SockseekApiJson.CreateSerializerOptions());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }
}
