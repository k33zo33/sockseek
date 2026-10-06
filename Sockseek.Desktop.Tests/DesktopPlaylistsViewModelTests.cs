using System.Diagnostics;
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
                return new PlaylistDownloadMissingResultDto(1, 0, 0, CreateResolution(1, downloading: 1), CreateDetail(playlistId, itemId, "Actions", "Downloading"), [CreateSubmission(itemId)]);
            if (request.Method == HttpMethod.Post && path.EndsWith("/cancel-active-downloads", StringComparison.Ordinal))
                return new PlaylistCancelDownloadsResultDto(1, 0, CreateResolution(1, failed: 1), CreateDetail(playlistId, itemId, "Actions", "Failed"));
            if (request.Method == HttpMethod.Post && path.EndsWith("/retry-failed", StringComparison.Ordinal))
                return new PlaylistDownloadMissingResultDto(1, 0, 0, CreateResolution(1, downloading: 1), CreateDetail(playlistId, itemId, "Actions", "Downloading"), [CreateSubmission(itemId)]);
            if (request.Method == HttpMethod.Post && path.EndsWith("/play-available", StringComparison.Ordinal))
                return CreatePlayerState(itemId, queueCount: 1);
            if (request.Method == HttpMethod.Post && path.EndsWith("/play-from-here", StringComparison.Ordinal))
                return CreatePlayerState(itemId, queueCount: 1);
            if (request.Method == HttpMethod.Post && path.EndsWith("/skip", StringComparison.Ordinal))
                return CreateDetail(playlistId, itemId, "Actions", "Skipped");
            if (request.Method == HttpMethod.Post && path.EndsWith("/retry", StringComparison.Ordinal))
                return new PlaylistDownloadMissingResultDto(1, 0, 0, CreateResolution(1, downloading: 1), CreateDetail(playlistId, itemId, "Actions", "Downloading"), [CreateSubmission(itemId)]);
            if (request.Method == HttpMethod.Post && path.EndsWith("/download", StringComparison.Ordinal))
                return new PlaylistDownloadMissingResultDto(1, 0, 0, CreateResolution(1, downloading: 1), CreateDetail(playlistId, itemId, "Actions", "Downloading"), [CreateSubmission(itemId)]);
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
        viewModel.DownloadProfileName = " lossless ";

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
        Assert.AreEqual("Downloading", viewModel.SelectedPlaylistItems[0].Status);

        Assert.IsTrue(await viewModel.CancelActiveDownloadsAsync());
        Assert.AreEqual("1 cancelled, 0 failed", viewModel.OperationSummary);
        Assert.AreEqual("Failed", viewModel.SelectedPlaylistItems[0].Status);

        Assert.IsTrue(await viewModel.RetryFailedAsync());
        Assert.AreEqual("1 retries submitted, 0 failed", viewModel.OperationSummary);
        Assert.AreEqual("Downloading", viewModel.SelectedPlaylistItems[0].Status);

        Assert.IsTrue(await viewModel.PlayAvailableAsync());
        Assert.AreEqual("1 available queued", viewModel.OperationSummary);

        Assert.IsTrue(await viewModel.PlayFromHereAsync(itemId));
        Assert.AreEqual("1 available queued from here", viewModel.OperationSummary);

        Assert.IsTrue(await viewModel.SkipItemAsync(itemId));
        Assert.AreEqual("Item skipped", viewModel.OperationSummary);
        Assert.AreEqual("Skipped", viewModel.SelectedPlaylistItems[0].Status);

        Assert.IsTrue(await viewModel.RetryItemAsync(itemId));
        Assert.AreEqual("1 retry submitted, 0 failed", viewModel.OperationSummary);
        Assert.AreEqual("Downloading", viewModel.SelectedPlaylistItems[0].Status);

        Assert.IsTrue(await viewModel.PlayItemAsync(itemId));
        Assert.AreEqual("Item is already resolving", viewModel.OperationSummary);
        AssertRequestBodyContains(handler, "/download-missing", "lossless");
        AssertRequestBodyContains(handler, "/retry-failed", "lossless");
        AssertRequestBodyContains(handler, "/retry", "lossless");
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
                $"api/v1/playlists/{playlistId}/retry-failed",
                $"api/v1/playlists/{playlistId}/play-available",
                $"api/v1/playlists/{playlistId}/items/{itemId}/play-from-here",
                $"api/v1/playlists/{playlistId}/items/{itemId}/skip",
                $"api/v1/playlists/{playlistId}/items/{itemId}/retry",
            },
            handler.Requests.ToArray());
    }

    [TestMethod]
    public async Task PlayItemAsync_UnresolvedItem_SubmitsItemDownloadWorkflow()
    {
        var playlistId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/playlists", StringComparison.Ordinal))
                return new[] { CreateSummary(playlistId, "Resolve Play") };
            if (request.Method == HttpMethod.Post && path.EndsWith("/download", StringComparison.Ordinal))
                return new PlaylistDownloadMissingResultDto(
                    1,
                    0,
                    0,
                    CreateResolution(1, downloading: 1),
                    CreateDetail(playlistId, itemId, "Resolve Play", "Downloading"),
                    [CreateSubmission(itemId)]);

            return CreateDetail(playlistId, itemId, "Resolve Play", "Unresolved");
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopPlaylistsViewModel(new SockseekApiClient(httpClient))
        {
            DownloadProfileName = " portable "
        };

        Assert.IsTrue(await viewModel.RefreshAsync());
        Assert.IsTrue(viewModel.SelectedPlaylistItems[0].CanPlay);
        Assert.IsFalse(viewModel.SelectedPlaylistItems[0].CanPlayLocal);

        Assert.IsTrue(await viewModel.PlayItemAsync(itemId));

        Assert.AreEqual("1 download submitted for playback, 0 failed", viewModel.OperationSummary);
        Assert.AreEqual("Downloading", viewModel.SelectedPlaylistItems[0].Status);
        AssertRequestBodyContains(handler, "/download", "portable");
        CollectionAssert.AreEqual(
            new[]
            {
                "api/v1/playlists",
                $"api/v1/playlists/{playlistId}",
                $"api/v1/playlists/{playlistId}/items/{itemId}/download",
            },
            handler.Requests.ToArray());
    }

    [TestMethod]
    public async Task SelectedPlaylistItemActions_CallExistingItemEndpointsForEligibleItems()
    {
        var playlistId = Guid.NewGuid();
        var availableItemId = Guid.NewGuid();
        var missingItemId = Guid.NewGuid();
        var failedItemId = Guid.NewGuid();
        var skippedItemId = Guid.NewGuid();
        var removedItemId = Guid.NewGuid();
        var statuses = new Dictionary<Guid, string>
        {
            [availableItemId] = "AvailableLocal",
            [missingItemId] = "Unresolved",
            [failedItemId] = "Failed",
            [skippedItemId] = "Skipped",
            [removedItemId] = "RemovedFromSourcePlaylist",
        };
        PlaylistDetailDto CreateDetail() => CreateBulkSelectionDetail(
            playlistId,
            statuses,
            availableItemId,
            missingItemId,
            failedItemId,
            skippedItemId,
            removedItemId);
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/playlists", StringComparison.Ordinal))
                return new[] { CreateSummary(playlistId, "Selected") };
            if (request.Method == HttpMethod.Post && path.EndsWith($"/items/{missingItemId}/download", StringComparison.Ordinal))
            {
                statuses[missingItemId] = "Downloading";
                return new PlaylistDownloadMissingResultDto(
                    1,
                    0,
                    0,
                    CreateResolution(statuses.Count, downloading: 1),
                    CreateDetail(),
                    [CreateSubmission(missingItemId)]);
            }

            if (request.Method == HttpMethod.Post && path.EndsWith($"/items/{failedItemId}/retry", StringComparison.Ordinal))
            {
                statuses[failedItemId] = "Downloading";
                return new PlaylistDownloadMissingResultDto(
                    1,
                    0,
                    0,
                    CreateResolution(statuses.Count, downloading: 1),
                    CreateDetail(),
                    [CreateSubmission(failedItemId)]);
            }

            if (request.Method == HttpMethod.Post && path.EndsWith($"/items/{skippedItemId}/retry", StringComparison.Ordinal))
            {
                statuses[skippedItemId] = "Downloading";
                return new PlaylistDownloadMissingResultDto(
                    1,
                    0,
                    0,
                    CreateResolution(statuses.Count, downloading: 1),
                    CreateDetail(),
                    [CreateSubmission(skippedItemId)]);
            }

            if (request.Method == HttpMethod.Post && path.EndsWith($"/items/{availableItemId}/skip", StringComparison.Ordinal))
            {
                statuses[availableItemId] = "Skipped";
                return CreateDetail();
            }

            return CreateDetail();
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopPlaylistsViewModel(new SockseekApiClient(httpClient))
        {
            DownloadProfileName = " selected-profile "
        };

        Assert.IsTrue(await viewModel.RefreshAsync());
        var available = viewModel.SelectedPlaylist!.Items.Single(item => item.PlaylistItemId == availableItemId);
        var missing = viewModel.SelectedPlaylist.Items.Single(item => item.PlaylistItemId == missingItemId);
        available.IsSelected = true;
        missing.IsSelected = true;
        Assert.AreEqual("5/5 tracks, 2 selected", viewModel.SelectedPlaylistItemSummary);
        Assert.IsTrue(viewModel.CanRunSelectedItemActions);

        Assert.IsTrue(await viewModel.DownloadSelectedAsync());

        Assert.AreEqual("1 selected downloads submitted, 0 failed, 0 skipped", viewModel.OperationSummary);
        Assert.IsTrue(handler.Requests.Any(request => request.EndsWith($"/items/{missingItemId}/download", StringComparison.Ordinal)));
        Assert.IsFalse(handler.Requests.Any(request => request.EndsWith($"/items/{availableItemId}/download", StringComparison.Ordinal)));
        AssertRequestBodyContains(handler, $"/items/{missingItemId}/download", "selected-profile");

        ClearSelectedPlaylistItems(viewModel);
        var failed = viewModel.SelectedPlaylist!.Items.Single(item => item.PlaylistItemId == failedItemId);
        var skipped = viewModel.SelectedPlaylist.Items.Single(item => item.PlaylistItemId == skippedItemId);
        failed.IsSelected = true;
        skipped.IsSelected = true;

        Assert.IsTrue(await viewModel.RetrySelectedAsync());

        Assert.AreEqual("2 selected retries submitted, 0 failed", viewModel.OperationSummary);
        Assert.IsTrue(handler.Requests.Any(request => request.EndsWith($"/items/{failedItemId}/retry", StringComparison.Ordinal)));
        Assert.IsTrue(handler.Requests.Any(request => request.EndsWith($"/items/{skippedItemId}/retry", StringComparison.Ordinal)));
        AssertRequestBodyContains(handler, $"/items/{failedItemId}/retry", "selected-profile");
        AssertRequestBodyContains(handler, $"/items/{skippedItemId}/retry", "selected-profile");

        ClearSelectedPlaylistItems(viewModel);
        available = viewModel.SelectedPlaylist!.Items.Single(item => item.PlaylistItemId == availableItemId);
        var removed = viewModel.SelectedPlaylist.Items.Single(item => item.PlaylistItemId == removedItemId);
        available.IsSelected = true;
        removed.IsSelected = true;

        Assert.IsTrue(await viewModel.SkipSelectedAsync());

        Assert.AreEqual("1 selected skipped, 0 failed", viewModel.OperationSummary);
        Assert.IsTrue(handler.Requests.Any(request => request.EndsWith($"/items/{availableItemId}/skip", StringComparison.Ordinal)));
        Assert.IsFalse(handler.Requests.Any(request => request.EndsWith($"/items/{removedItemId}/skip", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task SelectedPlaylistItems_AppliesStatusFilterAndSearchText()
    {
        var playlistId = Guid.NewGuid();
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/playlists", StringComparison.Ordinal))
                return new[] { CreateSummary(playlistId, "Filters") };

            return CreateMixedDetail(playlistId);
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopPlaylistsViewModel(new SockseekApiClient(httpClient));

        Assert.IsTrue(await viewModel.RefreshAsync());
        Assert.AreEqual(7, viewModel.SelectedPlaylistItems.Count);
        Assert.AreEqual("7/7 tracks", viewModel.SelectedPlaylistItemSummary);
        Assert.AreEqual("1/7 available, 1 missing, 1 failed, 1 skipped, 1 removed", viewModel.SelectedPlaylist!.ResolutionSummary);

        viewModel.IsMissingFilter = true;
        Assert.AreEqual(1, viewModel.SelectedPlaylistItems.Count);
        Assert.AreEqual("Missing Track", viewModel.SelectedPlaylistItems[0].Title);
        Assert.AreEqual("1/7 tracks", viewModel.SelectedPlaylistItemSummary);

        viewModel.IsReviewFilter = true;
        Assert.AreEqual(1, viewModel.SelectedPlaylistItems.Count);
        Assert.AreEqual("Review Track", viewModel.SelectedPlaylistItems[0].Title);

        viewModel.IsAllFilter = true;
        viewModel.PlaylistSearchText = "broken";
        Assert.AreEqual(1, viewModel.SelectedPlaylistItems.Count);
        Assert.AreEqual("Broken Track", viewModel.SelectedPlaylistItems[0].Title);

        viewModel.IsDownloadingFilter = true;
        viewModel.PlaylistSearchText = string.Empty;
        Assert.AreEqual(1, viewModel.SelectedPlaylistItems.Count);
        Assert.AreEqual("Download Track", viewModel.SelectedPlaylistItems[0].Title);

        viewModel.IsFailedFilter = true;
        Assert.AreEqual(1, viewModel.SelectedPlaylistItems.Count);
        Assert.AreEqual("Broken Track", viewModel.SelectedPlaylistItems[0].Title);

        viewModel.IsSkippedFilter = true;
        Assert.AreEqual(1, viewModel.SelectedPlaylistItems.Count);
        Assert.AreEqual("Skipped Track", viewModel.SelectedPlaylistItems[0].Title);

        viewModel.IsRemovedFilter = true;
        Assert.AreEqual(1, viewModel.SelectedPlaylistItems.Count);
        Assert.AreEqual("Removed Track", viewModel.SelectedPlaylistItems[0].Title);
        Assert.IsFalse(viewModel.SelectedPlaylistItems[0].CanPlay);
        Assert.IsFalse(viewModel.SelectedPlaylistItems[0].CanSkip);
    }

    [TestMethod]
    public async Task SelectedPlaylistItems_FiltersTenThousandItemsWithinBudget()
    {
        const int itemCount = 10_000;
        var playlistId = Guid.NewGuid();
        var handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/playlists", StringComparison.Ordinal))
                return new[] { CreateSummary(playlistId, "Large Filters") };

            return CreateLargeDetail(playlistId, itemCount);
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopPlaylistsViewModel(new SockseekApiClient(httpClient));

        Assert.IsTrue(await viewModel.RefreshAsync());
        Assert.AreEqual(itemCount, viewModel.SelectedPlaylistItems.Count);
        Assert.AreSame(viewModel.SelectedPlaylistItems, viewModel.SelectedPlaylistItems);

        var stopwatch = Stopwatch.StartNew();
        viewModel.PlaylistSearchText = "needle";
        var searchedItems = viewModel.SelectedPlaylistItems;
        stopwatch.Stop();

        Assert.AreEqual(200, searchedItems.Count);
        Assert.AreEqual("200/10000 tracks", viewModel.SelectedPlaylistItemSummary);
        Assert.IsTrue(
            stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"Expected search filtering to stay under 1 second, actual elapsed was {stopwatch.Elapsed}.");

        stopwatch.Restart();
        viewModel.IsFailedFilter = true;
        var failedItems = viewModel.SelectedPlaylistItems;
        stopwatch.Stop();

        Assert.AreEqual(100, failedItems.Count);
        Assert.IsTrue(
            stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"Expected status filtering to stay under 1 second, actual elapsed was {stopwatch.Elapsed}.");
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
        StringAssert.Contains(xaml, "Playlists.RetryFailedCommand");
        StringAssert.Contains(xaml, "Playlists.DownloadSelectedCommand");
        StringAssert.Contains(xaml, "Playlists.RetrySelectedCommand");
        StringAssert.Contains(xaml, "Playlists.SkipSelectedCommand");
        StringAssert.Contains(xaml, "Playlists.PlayFromHereCommand");
        StringAssert.Contains(xaml, "Playlists.ApproveLocalMatchCommand");
        StringAssert.Contains(xaml, "Playlists.RejectLocalMatchCommand");
        StringAssert.Contains(xaml, "Playlists.SkipItemCommand");
        StringAssert.Contains(xaml, "Playlists.RetryItemCommand");
        StringAssert.Contains(xaml, "Playlists.IsAvailableFilter");
        StringAssert.Contains(xaml, "Playlists.IsMissingFilter");
        StringAssert.Contains(xaml, "Playlists.IsSkippedFilter");
        StringAssert.Contains(xaml, "Playlists.IsRemovedFilter");
        StringAssert.Contains(xaml, "Playlists.DownloadProfileName");
        StringAssert.Contains(xaml, "Playlists.PlaylistSearchText");
        StringAssert.Contains(xaml, "Playlists.SelectedPlaylistItemSummary");
        StringAssert.Contains(xaml, "IsChecked=\"{Binding IsSelected}\"");
        StringAssert.Contains(xaml, "<ListBox ItemsSource=\"{Binding Playlists.SelectedPlaylistItems}\"");
        StringAssert.Contains(xaml, "MaxHeight=\"520\"");
    }

    private static void AssertRequestBodyContains(RecordingHandler handler, string pathSuffix, string expected)
    {
        var index = handler.Requests.FindIndex(request => request.EndsWith(pathSuffix, StringComparison.Ordinal));
        Assert.IsTrue(index >= 0, $"Expected a request ending with '{pathSuffix}'.");
        StringAssert.Contains(handler.RequestBodies[index] ?? string.Empty, expected);
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
                downloading: status == "Downloading" ? 1 : 0,
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

    private static PlaylistDetailDto CreateMixedDetail(Guid playlistId)
        => new(
            playlistId,
            "Filters",
            "Mirror",
            "spotify",
            "external-playlist",
            "https://example.test/playlist",
            new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 28, 8, 5, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 28, 8, 5, 0, TimeSpan.Zero),
            CreateResolution(7, available: 1, unresolved: 1, review: 1, downloading: 1, failed: 1, skipped: 1, removed: 1),
            [
                CreateItem(1, "Ready Track", "AvailableLocal", canonicalTrackId: Guid.NewGuid()),
                CreateItem(2, "Missing Track", "Unresolved"),
                CreateItem(3, "Download Track", "Downloading"),
                CreateItem(4, "Review Track", "ReviewRequired", canonicalTrackId: Guid.NewGuid()),
                CreateItem(5, "Broken Track", "Failed"),
                CreateItem(6, "Skipped Track", "Skipped"),
                CreateItem(7, "Removed Track", "RemovedFromSourcePlaylist"),
            ]);

    private static PlaylistDetailDto CreateLargeDetail(Guid playlistId, int itemCount)
    {
        var failedCount = itemCount / 100;
        return new(
            playlistId,
            "Large Filters",
            "Mirror",
            "spotify",
            "external-playlist",
            "https://example.test/playlist",
            new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 28, 8, 5, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 28, 8, 5, 0, TimeSpan.Zero),
            CreateResolution(itemCount, unresolved: itemCount - failedCount, failed: failedCount),
            Enumerable.Range(1, itemCount)
                .Select(position => CreateItem(
                    position,
                    position % 50 == 0 ? $"Needle Track {position}" : $"Track {position}",
                    position % 100 == 0 ? "Failed" : "Unresolved"))
                .ToArray());
    }

    private static PlaylistDetailDto CreateBulkSelectionDetail(
        Guid playlistId,
        IReadOnlyDictionary<Guid, string> statuses,
        Guid availableItemId,
        Guid missingItemId,
        Guid failedItemId,
        Guid skippedItemId,
        Guid removedItemId)
        => new(
            playlistId,
            "Selected",
            "Mirror",
            "spotify",
            "external-playlist",
            "https://example.test/playlist",
            new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 28, 8, 5, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 28, 8, 5, 0, TimeSpan.Zero),
            CreateResolution(
                statuses.Count,
                available: CountStatus(statuses, "AvailableLocal"),
                unresolved: CountStatus(statuses, "Unresolved"),
                downloading: CountStatus(statuses, "Downloading"),
                failed: CountStatus(statuses, "Failed"),
                skipped: CountStatus(statuses, "Skipped"),
                removed: CountStatus(statuses, "RemovedFromSourcePlaylist")),
            [
                CreateItem(availableItemId, 1, "Ready Track", statuses[availableItemId], canonicalTrackId: Guid.NewGuid()),
                CreateItem(missingItemId, 2, "Missing Track", statuses[missingItemId]),
                CreateItem(failedItemId, 3, "Failed Track", statuses[failedItemId]),
                CreateItem(skippedItemId, 4, "Skipped Track", statuses[skippedItemId]),
                CreateItem(removedItemId, 5, "Removed Track", statuses[removedItemId]),
            ]);

    private static PlaylistItemDto CreateItem(
        int position,
        string title,
        string status,
        Guid? canonicalTrackId = null)
        => CreateItem(Guid.NewGuid(), position, title, status, canonicalTrackId);

    private static PlaylistItemDto CreateItem(
        Guid playlistItemId,
        int position,
        string title,
        string status,
        Guid? canonicalTrackId = null)
        => new(
            playlistItemId,
            position,
            $"provider-item-{position}",
            canonicalTrackId,
            status,
            title,
            ["Artist"],
            "Album",
            180000,
            null,
            null,
            $"external-track-{position}",
            $"https://example.test/track/{position}",
            null,
            null);

    private static int CountStatus(IReadOnlyDictionary<Guid, string> statuses, string status)
        => statuses.Values.Count(value => string.Equals(value, status, StringComparison.Ordinal));

    private static void ClearSelectedPlaylistItems(DesktopPlaylistsViewModel viewModel)
    {
        foreach (var item in viewModel.SelectedPlaylist?.Items ?? [])
            item.IsSelected = false;
    }

    private static PlaylistResolutionSummaryDto CreateResolution(
        int total,
        int available = 0,
        int unresolved = 0,
        int review = 0,
        int searching = 0,
        int downloading = 0,
        int failed = 0,
        int skipped = 0,
        int removed = 0)
        => new(total, available, unresolved, review, searching, 0, downloading, failed, skipped, removed);

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
        public List<string?> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty);
            RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(statusCode) { Content = JsonContent.Create(responseFactory(request)) };
        }
    }
}
