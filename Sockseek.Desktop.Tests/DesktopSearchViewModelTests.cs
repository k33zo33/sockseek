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

    [TestMethod]
    public async Task RefreshResultsAsync_TrackMode_ExposesFileCandidatesAndRevision()
    {
        var jobId = Guid.NewGuid();
        var handler = new RecordingHandler(request => request.RequestUri?.AbsolutePath.EndsWith("/results/files") == true
            ? new SearchResultSnapshotDto<FileCandidateDto>(
                3,
                true,
                [new FileCandidateDto(
                    new FileCandidateRefDto("user", "folder/song.flac"),
                    "user",
                    "folder/song.flac",
                    new PeerInfoDto("user", true, 1000),
                    123,
                    320,
                    44100,
                    210)])
            : new JobSummaryDto { JobId = jobId });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSearchViewModel(new SockseekApiClient(httpClient))
        {
            Title = "Song"
        };

        await viewModel.SearchAsync();
        var refreshed = await viewModel.RefreshResultsAsync();

        Assert.IsTrue(refreshed);
        Assert.AreEqual(3, viewModel.ResultsRevision);
        Assert.IsTrue(viewModel.IsResultsComplete);
        Assert.AreEqual(1, viewModel.FileCandidates.Count);
        Assert.AreEqual("folder/song.flac", viewModel.FileCandidates[0].Filename);
        Assert.AreEqual("api/jobs/" + jobId + "/results/files", handler.RequestUri);
    }

    [TestMethod]
    public async Task DownloadFileAsync_SubmitsOnlyExplicitCandidateReference()
    {
        var searchJobId = Guid.NewGuid();
        var downloadJobId = Guid.NewGuid();
        var candidate = new FileCandidateDto(
            new FileCandidateRefDto("user", "folder/song.flac"),
            "user",
            "folder/song.flac",
            new PeerInfoDto("user"),
            123,
            null,
            null,
            null);
        var handler = new RecordingHandler(request => request.RequestUri?.AbsolutePath.EndsWith("/downloads/files") == true
            ? new List<JobSummaryDto> { new() { JobId = downloadJobId } }
            : new JobSummaryDto { JobId = searchJobId });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopSearchViewModel(new SockseekApiClient(httpClient))
        {
            Title = "Song"
        };

        var searchJob = await viewModel.SearchAsync();
        Assert.IsNotNull(searchJob);

        var downloaded = await viewModel.DownloadFileAsync(candidate);

        Assert.IsTrue(downloaded);
        Assert.AreEqual(downloadJobId, viewModel.LastDownloadJobs[0].JobId);
        Assert.AreEqual("api/jobs/" + searchJobId + "/downloads/files", handler.RequestUri);
        StringAssert.Contains(handler.RequestBody, "folder/song.flac");
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, object> responseFactory) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.PathAndQuery.TrimStart('/');
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(responseFactory(request)) };
        }
    }
}