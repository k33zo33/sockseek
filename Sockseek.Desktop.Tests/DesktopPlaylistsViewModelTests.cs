using System.Net;
using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopPlaylistsViewModelTests
{
    [TestMethod]
    public async Task RefreshAsync_LoadsPlaylistsAndSelectsFirstDetail()
    {
        var playlistId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/playlists", StringComparison.Ordinal))
                return new[] { CreateSummary(playlistId, "Road list") };

            return CreateDetail(playlistId, itemId, "Road list", "Unresolved");
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopPlaylistsViewModel(new SockseekApiClient(httpClient));

        Assert.IsTrue(await viewModel.RefreshAsync());

        CollectionAssert.AreEqual(new[] { "api/v1/playlists", $"api/v1/playlists/{playlistId}" }, handler.Requests.ToArray());
        Assert.AreEqual(1, viewModel.Playlists.Count);
        Assert.IsNotNull(viewModel.SelectedPlaylist);
        Assert.AreEqual("Road list", viewModel.SelectedPlaylist.Name);
        Assert.AreEqual(1, viewModel.SelectedPlaylistItems.Count);
        Assert.AreEqual("Unresolved", viewModel.SelectedPlaylistItems[0].Status);
        Assert.IsNull(viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task PlaylistActions_CallBackendAndUpdateSelectedDetail()
    {
        var playlistId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/playlists", StringComparison.Ordinal))
                return new[] { CreateSummary(playlistId, "Actions") };
            if (request.Method == HttpMethod.Post && path.EndsWith("/resolve-local", StringComparison.Ordinal))
                return new PlaylistLocalResolveResultDto(1, 0, 0, CreateResolution(1, 1, 0), CreateDetail(playlistId, itemId, "Actions", "AvailableLocal", canonicalTrackId: Guid.NewGuid()));
            if (request.Method == HttpMethod.Post && path.EndsWith("/download-missing", StringComparison.Ordinal))
                return new PlaylistDownloadMissingResultDto(1, 0, 0, CreateResolution(1, searching: 1), CreateDetail(playlistId, itemId, "Actions", "Searching"), [CreateSubmission(itemId)]);
            if (request.Method == HttpMethod.Post && path.EndsWith("/cancel-active-downloads", StringComparison.Ordinal))
                return new PlaylistCancelDownloadsResultDto(1, 0, CreateResolution(1, failed: 1), CreateDetail(playlistId, itemId, "Actions", "Failed"));
            if (request.Method == HttpMethod.Post && path.EndsWith("/play-available", StringComparison.Ordinal))
                return CreatePlayerState(itemId, queueCount: 1);
            if (request.Method == HttpMethod.Post && path.EndsWith("/play-from-here", StringComparison.Ordinal))
                return CreatePlayerState(itemId, queueCount: 1);
            if (request.Method == HttpMethod.Post && path.EndsWith("/skip", StringComparison.Ordinal))
                return CreateDetail(playlistId, itemId, "Actions", "Skipped");
            if (request.Method == HttpMethod.Post && path.EndsWith("/retry", StringComparison.Ordinal))
                return new PlaylistDownloadMissingResultDto(1, 0, 0, CreateResolution(1, searching: 1), CreateDetail(playlistId, itemId, "Actions", "Searching"), [CreateSubmission(itemId)]);
            if (request.Method == HttpMethod.Post && path.EndsWith("/approve-local", StringComparison.Ordinal))
                return CreateDetail(playlistId, itemId, "Actions", "AvailableLocal", canonicalTrackId: Guid.NewGuid());
            if (request.Method == HttpMethod.Post && path.EndsWith("/reject-local", StringComparison.Ordinal))
                return CreateDetail(playlistId, itemId, "Actions", "Unresolved");
            if (request.Method == HttpMethod.Post && path.EndsWith("/player/play/playlist-item", StringComparison.Ordinal))
                return CreatePlayerState(itemId, queueCount: 0);

            return CreateDetail(playlistId, itemId, "Actions", "ReviewRequired", canonicalTrackId: Guid.NewGuid());
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopPlaylistsViewModel(new SockseekApiClient(httpClient));

        Assert.IsTrue(await viewModel.RefreshAsync());
        Assert.AreEqual("ReviewRequired", viewModel.SelectedPlaylistItems[0].Status);
        Assert.IsTrue(viewModel.SelectedPlaylistItems[0].CanReviewLocal);

        Assert.IsTrue(await viewModel.ApproveLocalMatchAsync(itemId));
        Assert.AreEqual("Local match approved", viewModel.OperationSummary);
        Assert.AreEqual("AvailableLocal", viewModel.SelectedPlaylistItems[0].Status);

        Assert.IsTrue(await viewModel.RejectLocalMatchAsync(itemId));
        Assert.AreEqual("Local match rejected", viewModel.OperationSummary);
        Assert.AreEqual("Unresolved", viewModel.SelectedPlaylistItems[0].Status);

        Assert.IsTrue(await viewModel.ResolveLocalAsync());
        Assert.AreEqual("1 matched, 0 review, 0 unresolved", viewModel.OperationSummary);
        Assert.AreEqual("AvailableLocal", viewModel.SelectedPlaylistItems[0].Status);

        Assert.IsTrue(await viewModel.DownloadMissingAsync());
        Assert.AreEqual("1 submitted, 0 failed, 0 skipped", viewModel.OperationSummary);
        Assert.AreEqual("Searching", viewModel.SelectedPlaylistItems[0].Status);

        Assert.IsTrue(await viewModel.CancelActiveDownloadsAsync());
        Assert.AreEqual("1 cancelled, 0 failed", viewModel.OperationSummary);
        Assert.AreEqual("Failed", viewModel.SelectedPlaylistItems[0].Status);

        Assert.IsTrue(await viewModel.PlayAvailableAsync());
        Assert.AreEqual("1 available queued", viewModel.OperationSummary);

        Assert.IsTrue(await viewModel.PlayFromHereAsync(itemId));
        Assert.AreEqual("1 available queued from here", viewModel.OperationSummary);

        Assert.IsTrue(await viewModel.SkipItemAsync(itemId));
        Assert.AreEqual("Item skipped", viewModel.OperationSummary);
        Assert.AreEqual("Skipped", viewModel.SelectedPlaylistItems[0].Status);

        Assert.IsTrue(await viewModel.RetryItemAsync(itemId));
        Assert.AreEqual("1 retry submitted, 0 failed", viewModel.OperationSummary);
        Assert.AreEqual("Searching", viewModel.SelectedPlaylistItems[0].Status);

        Assert.IsTrue(await viewModel.PlayItemAsync(itemId));
        Assert.AreEqual("Playback started", viewModel.OperationSummary);
        CollectionAssert.AreEqual(
            new[]
            {
                "api/v1/playlists",
                $"api/v1/playlists/{playlistId}",
                $"api/v1/playlists/{playlistId}/items/{itemId}/approve-local",
                $"api/v1/playlists/{playlistId}/items/{itemId}/reject-local",
                $"api/v1/playlists/{playlistId}/resolve-local",
                $"api/v1/playlists/{playlistId}/download-missing",
                $"api/v1/playlists/{playlistId}/cancel-active-downloads",
                $"api/v1/playlists/{playlistId}/play-available",
                $"api/v1/playlists/{playlistId}/items/{itemId}/play-from-here",
                $"api/v1/playlists/{playlistId}/items/{itemId}/skip",
                $"api/v1/playlists/{playlistId}/items/{itemId}/retry",
                "api/v1/player/play/playlist-item",
            },
            handler.Requests.ToArray());
    }

    [TestMethod]
    public void PlaylistsSurface_BindsListDetailAndCommands()
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

        StringAssert.Contains(xaml, "IsVisible=\"{Binding IsPlaylistsVisible}\"");
        StringAssert.Contains(xaml, "ItemsSource=\"{Binding Playlists.Playlists}\"");
        StringAssert.Contains(xaml, "Playlists.PlayAvailableCommand");
        StringAssert.Contains(xaml, "Playlists.ResolveLocalCommand");
        StringAssert.Contains(xaml, "Playlists.DownloadMissingCommand");
        StringAssert.Contains(xaml, "Playlists.CancelActiveDownloadsCommand");
        StringAssert.Contains(xaml, "Playlists.PlayFromHereCommand");
        StringAssert.Contains(xaml, "Playlists.ApproveLocalMatchCommand");
        StringAssert.Contains(xaml, "Playlists.RejectLocalMatchCommand");
        StringAssert.Contains(xaml, "Playlists.SkipItemCommand");
        StringAssert.Contains(xaml, "Playlists.RetryItemCommand");
    }

    private static PlaylistSummaryDto CreateSummary(Guid playlistId, string name)
        => new(
            playlistId,
            name,
            "Mirror",
            "spotify",
            "external-playlist",
            "https://example.test/playlist",
            new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 28, 8, 5, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 28, 8, 5, 0, TimeSpan.Zero),
            CreateResolution(1, unresolved: 1));

    private static PlaylistDetailDto CreateDetail(
        Guid playlistId,
        Guid itemId,
        string name,
        string status,
        Guid? canonicalTrackId = null)
        => new(
            playlistId,
            name,
            "Mirror",
            "spotify",
            "external-playlist",
            "https://example.test/playlist",
            new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 28, 8, 5, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 28, 8, 5, 0, TimeSpan.Zero),
            CreateResolution(
                1,
                available: status == "AvailableLocal" ? 1 : 0,
                unresolved: status == "Unresolved" ? 1 : 0,
                review: status == "ReviewRequired" ? 1 : 0,
                searching: status == "Searching" ? 1 : 0,
                failed: status == "Failed" ? 1 : 0,
                skipped: status == "Skipped" ? 1 : 0),
            [
                new PlaylistItemDto(
                    itemId,
                    1,
                    "provider-item-1",
                    canonicalTrackId,
                    status,
                    "Track",
                    ["Artist"],
                    "Album",
                    180000,
                    "USRC17607839",
                    "mbid-1",
                    "external-track-1",
                    "https://example.test/track",
                    null,
                    null),
            ]);

    private static PlaylistResolutionSummaryDto CreateResolution(
        int total,
        int available = 0,
        int unresolved = 0,
        int review = 0,
        int searching = 0,
        int downloading = 0,
        int failed = 0,
        int skipped = 0)
        => new(total, available, unresolved, review, searching, 0, downloading, failed, skipped, 0);

    private static PlaylistDownloadSubmissionDto CreateSubmission(Guid itemId)
        => new(itemId, Guid.NewGuid(), Guid.NewGuid());

    private static PlayerStateDto CreatePlayerState(Guid itemId, int queueCount)
    {
        var queueItems = Enumerable.Range(0, queueCount)
            .Select(_ => new PlayerQueueItemDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null))
            .ToArray();
        return new PlayerStateDto(
            "Playing",
            Guid.NewGuid(),
            itemId,
            Guid.NewGuid(),
            "C:/Music/track.flac",
            null,
            0,
            1.0,
            false,
            new PlayerQueueDto(queueItems, queueItems.Length == 0 ? -1 : 0, "None", false, 0, []));
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
