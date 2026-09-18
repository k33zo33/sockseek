using System.Net;
using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopLibraryViewModelTests
{
    [TestMethod]
    public async Task RefreshRootsAsync_LoadsConfiguredRoots()
    {
        var rootId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new[]
        {
            new LibraryRootDto(rootId, "C:/Music/", "Music", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null)
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopLibraryViewModel(new SockseekApiClient(httpClient));

        Assert.IsTrue(await viewModel.RefreshRootsAsync());

        Assert.AreEqual("api/v1/library/roots", handler.RequestUri);
        Assert.AreEqual(1, viewModel.Roots.Count);
        Assert.AreEqual(rootId, viewModel.Roots[0].Id);
        Assert.IsNull(viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task SaveRootAsync_ValidatesAndPostsRoot()
    {
        var rootId = Guid.NewGuid();
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
                return new LibraryRootDto(rootId, "C:/Music/", "Music", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null);

            return new[]
            {
                new LibraryRootDto(rootId, "C:/Music/", "Music", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null)
            };
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopLibraryViewModel(new SockseekApiClient(httpClient))
        {
            RootPath = " C:/Music ",
            RootDisplayName = " Music "
        };

        var saved = await viewModel.SaveRootAsync();

        Assert.IsNotNull(saved);
        Assert.AreEqual(rootId, saved.Id);
        CollectionAssert.AreEqual(new[] { "api/v1/library/roots", "api/v1/library/roots" }, handler.Requests.ToArray());
        StringAssert.Contains(handler.RequestBodies[0], "C:/Music");
        StringAssert.Contains(handler.RequestBodies[0], "Music");
        Assert.AreEqual(string.Empty, viewModel.RootPath);
        Assert.AreEqual(string.Empty, viewModel.RootDisplayName);
    }

    [TestMethod]
    public async Task SaveRootAsync_WithoutPath_ReturnsValidationErrorWithoutRequest()
    {
        var handler = new RecordingHandler(_ => Array.Empty<LibraryRootDto>());
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopLibraryViewModel(new SockseekApiClient(httpClient));

        var saved = await viewModel.SaveRootAsync();

        Assert.IsNull(saved);
        StringAssert.Contains(viewModel.ErrorMessage, "root path");
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task SearchAsync_LoadsLocalTrackRows()
    {
        var trackId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new LocalLibrarySearchResponseDto(
            1,
            [new LocalLibraryTrackDto(
                trackId,
                "Artist",
                "Track",
                "Album",
                210000,
                null,
                null,
                1,
                0,
                Guid.NewGuid(),
                "C:/Music/Track.flac",
                "flac",
                900,
                48000,
                24)]));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopLibraryViewModel(new SockseekApiClient(httpClient))
        {
            SearchText = "track",
            IncludeMissing = false
        };

        Assert.IsTrue(await viewModel.SearchAsync());

        Assert.AreEqual("api/v1/library/tracks?offset=0&limit=100&includeMissing=false&searchText=track", handler.RequestUri);
        Assert.AreEqual(1, viewModel.TotalTrackCount);
        Assert.AreEqual(1, viewModel.Tracks.Count);
        Assert.AreEqual("Track", viewModel.Tracks[0].Title);
        Assert.AreEqual("Album", viewModel.Tracks[0].AlbumTitle);
        Assert.AreEqual(1, viewModel.AlbumGroups.Count);
        Assert.AreEqual("Artist - Album", viewModel.AlbumGroups[0].DisplayTitle);
        Assert.AreEqual("1 available / 0 missing", viewModel.Tracks[0].AvailabilitySummary);
        Assert.AreEqual("3:30 | flac | 900 kbps | 48000 Hz | 24 bit", viewModel.Tracks[0].TechnicalSummary);
    }

    [TestMethod]
    public async Task ScanAsync_TriggersScanAndRefreshesSearchAndRoots()
    {
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/scan", StringComparison.Ordinal))
            {
                return new LocalLibraryConfiguredScanResultDto(
                    [Guid.NewGuid()],
                    new LocalLibraryScanResultDto(2, 2, 2, 0, 0, 0));
            }

            if (path.EndsWith("/tracks", StringComparison.Ordinal))
                return new LocalLibrarySearchResponseDto(0, []);

            return Array.Empty<LibraryRootDto>();
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopLibraryViewModel(new SockseekApiClient(httpClient));

        Assert.IsTrue(await viewModel.ScanAsync());

        CollectionAssert.AreEqual(
            new[] { "api/v1/library/scan", "api/v1/library/tracks?offset=0&limit=100&includeMissing=true", "api/v1/library/roots" },
            handler.Requests.ToArray());
        Assert.AreEqual("2 imported, 0 missing, 0 failed", viewModel.ScanSummary);
    }

    [TestMethod]
    public async Task RefreshDuplicatesAsync_LoadsDuplicateGroups()
    {
        var handler = new RecordingHandler(_ => new[]
        {
            new LocalLibraryDuplicateGroupDto(
                Guid.NewGuid(),
                "Artist",
                "Track",
                180000,
                2,
                [
                    new LocalLibraryDuplicateFileDto(Guid.NewGuid(), "C:/A.flac", 1, 180000, "flac", 900, 48000, 24, LocalMediaAvailabilityDto.Available),
                    new LocalLibraryDuplicateFileDto(Guid.NewGuid(), "C:/B.flac", 1, 180000, "flac", 900, 48000, 24, LocalMediaAvailabilityDto.Available)
                ])
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopLibraryViewModel(new SockseekApiClient(httpClient));

        Assert.IsTrue(await viewModel.RefreshDuplicatesAsync());

        Assert.AreEqual("api/v1/library/duplicates?limit=100&includeMissing=true", handler.RequestUri);
        Assert.AreEqual(1, viewModel.DuplicateGroups.Count);
        Assert.AreEqual("Artist - Track", viewModel.DuplicateGroups[0].DisplayTitle);
        Assert.AreEqual(2, viewModel.DuplicateGroups[0].Paths.Count);
    }

    [TestMethod]
    public void LibraryTrackList_UsesBoundedListBox()
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

        StringAssert.Contains(xaml, "<ListBox ItemsSource=\"{Binding Library.Tracks}\"");
        StringAssert.Contains(xaml, "MaxHeight=\"420\"");
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, object> responseFactory,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? RequestUri => Requests.LastOrDefault();

        public List<string> Requests { get; } = [];

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty);
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(statusCode) { Content = JsonContent.Create(responseFactory(request)) };
        }
    }
}
