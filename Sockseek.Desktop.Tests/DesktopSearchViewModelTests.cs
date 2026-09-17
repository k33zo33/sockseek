using System.Net;
using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopSearchViewModelTests
{
    [TestMethod]
    public async Task SearchAsync_TrackMode_SubmitsTrimmedTypedQuery()
    {
        var handler = new RecordingHandler(_ => new JobSummaryDto { DisplayId = 42 });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSearchViewModel(new SockseekApiClient(httpClient))
        {
            Artist = "  Artist ",
            Title = " Title ",
            Album = " Album "
        };

        var job = await viewModel.SearchAsync();

        Assert.IsNotNull(job);
        Assert.AreEqual(42, job.DisplayId);
        StringAssert.Contains(handler.RequestBody, "Artist");
        StringAssert.Contains(handler.RequestBody, "Title");
        StringAssert.Contains(handler.RequestBody, "Album");
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsNull(viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task SearchAsync_AlbumMode_SubmitsAlbumQuery()
    {
        var handler = new RecordingHandler(_ => new JobSummaryDto { DisplayId = 43 });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSearchViewModel(new SockseekApiClient(httpClient))
        {
            Mode = DesktopSearchMode.Album,
            Artist = "Artist",
            Album = "Album",
            SearchHint = "Track"
        };

        var job = await viewModel.SearchAsync();

        Assert.IsNotNull(job);
        Assert.AreEqual("api/jobs/search/albums", handler.RequestUri);
        StringAssert.Contains(handler.RequestBody, "SearchHint");
    }

    [TestMethod]
    public async Task SearchAsync_WithoutRequiredQuery_ReturnsValidationErrorWithoutRequest()
    {
        var handler = new RecordingHandler(_ => new JobSummaryDto());
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSearchViewModel(new SockseekApiClient(httpClient));

        var job = await viewModel.SearchAsync();

        Assert.IsNull(job);
        StringAssert.Contains(viewModel.ErrorMessage, "artist or title");
        Assert.IsNull(handler.RequestUri);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, JobSummaryDto> responseFactory) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.PathAndQuery.TrimStart('/');
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseFactory(request))
            };
        }
    }
}