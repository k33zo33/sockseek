using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Domain.Tracks;
using Sockseek.Domain.Workflows;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;
using Sockseek.Infrastructure.Security;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class PlaylistEndpointTests
{
    [TestMethod]
    public async Task GetPlaylistsAndDetail_ReturnImportedPlaylistSummaryAndItems()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var playlistId = await SeedPlaylistAsync(app);
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var playlists = await client.GetPlaylistsAsync();
            var detail = await client.GetPlaylistAsync(playlistId);
            var missing = await client.GetPlaylistAsync(Guid.NewGuid());

            Assert.AreEqual(1, playlists.Count);
            Assert.IsNotNull(detail);
            Assert.IsNull(missing);
            Assert.AreEqual(playlistId, playlists.Single().PlaylistId);
            Assert.AreEqual(playlistId, detail.PlaylistId);
            Assert.AreEqual("Daily Mix", detail.Name);
            Assert.AreEqual("spotify", detail.ProviderId);
            Assert.AreEqual("playlist-1", detail.ExternalPlaylistId);
            Assert.AreEqual("Mirror", detail.ImportMode);
            Assert.AreEqual(3, detail.Resolution.TotalItems);
            Assert.AreEqual(1, detail.Resolution.AvailableLocalItems);
            Assert.AreEqual(1, detail.Resolution.UnresolvedItems);
            Assert.AreEqual(1, detail.Resolution.RemovedItems);
            Assert.AreEqual(3, detail.Items.Count);
            Assert.AreEqual("Track One", detail.Items[0].Title);
            CollectionAssert.AreEqual(new[] { "Artist One", "Guest Artist" }, detail.Items[0].Artists.ToArray());
            Assert.AreEqual("USRC17607839", detail.Items[0].Isrc);
            Assert.AreEqual("RemovedFromSourcePlaylist", detail.Items[2].Status);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetPlaylists_RequiresSessionToken()
    {
        var app = CreateApp(out var url, out _, out var tempRoot);
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(url) };

            using var response = await http.GetAsync("/api/v1/playlists");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task ResolvePlaylistLocal_MatchesAvailableLocalFilesAndReturnsUpdatedSummary()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var playlistId = await SeedUnresolvedPlaylistWithLocalMatchAsync(app);
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var result = await client.ResolvePlaylistLocalAsync(playlistId);
            var missing = await client.ResolvePlaylistLocalAsync(Guid.NewGuid());

            Assert.IsNotNull(result);
            Assert.IsNull(missing);
            Assert.AreEqual(1, result.MatchedItems);
            Assert.AreEqual(0, result.ReviewItems);
            Assert.AreEqual(1, result.UnresolvedItems);
            Assert.AreEqual(2, result.Resolution.TotalItems);
            Assert.AreEqual(1, result.Resolution.AvailableLocalItems);
            Assert.AreEqual(1, result.Resolution.UnresolvedItems);
            Assert.AreEqual("AvailableLocal", result.Playlist.Items[0].Status);
            Assert.IsNotNull(result.Playlist.Items[0].CanonicalTrackId);
            Assert.AreEqual("Unresolved", result.Playlist.Items[1].Status);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task DownloadMissingPlaylistItems_SubmitsDownloadWorkflowsAndPersistsPlaylistLinks()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var playlistId = await SeedMissingPlaylistAsync(app);
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var result = await client.DownloadMissingPlaylistItemsAsync(playlistId);
            var missing = await client.DownloadMissingPlaylistItemsAsync(Guid.NewGuid());

            Assert.IsNotNull(result);
            Assert.IsNull(missing);
            Assert.AreEqual(2, result.SubmittedItems);
            Assert.AreEqual(0, result.FailedItems);
            Assert.AreEqual(2, result.Submissions.Count);
            Assert.AreEqual(4, result.Resolution.TotalItems);
            Assert.AreEqual(2, result.Resolution.DownloadingItems);
            Assert.AreEqual(1, result.Resolution.AvailableLocalItems);
            Assert.AreEqual(1, result.Resolution.RemovedItems);

            await using var verifyScope = app.Services.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            var workflows = await db.DownloadWorkflows
                .AsNoTracking()
                .ToListAsync();
            workflows = workflows
                .OrderBy(workflow => workflow.CreatedAtUtc)
                .ToList();
            Assert.AreEqual(2, workflows.Count);
            CollectionAssert.AreEquivalent(
                result.Submissions.Select(submission => submission.EngineJobId).ToArray(),
                workflows.Select(workflow => workflow.EngineJobId).ToArray());
            Assert.IsTrue(workflows.All(workflow => workflow.PlaylistItemId.HasValue));
            Assert.IsTrue(workflows.All(workflow => workflow.Status == (int)DownloadWorkflowPersistenceStatus.Downloading));
            Assert.IsTrue(await db.PlaylistItems.CountAsync(item => item.PlaylistId == playlistId && item.Status == (int)PlaylistItemStatus.Downloading) == 2);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task GetPlaylist_SyncsCompletedDownloadWorkflowIntoLocalAvailability()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        SeedMockSoulseekFile(tempRoot, "Artist One", "Album One", "01. Artist One - First Missing.mp3");
        SeedMockSoulseekFile(tempRoot, "Artist Two", "Album Two", "02. Artist Two - Second Missing.mp3");
        await app.StartAsync();
        try
        {
            var playlistId = await SeedMissingPlaylistAsync(app);
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var submitted = await client.DownloadMissingPlaylistItemsAsync(playlistId);
            var synced = await WaitForPlaylistSummaryAsync(
                client,
                playlistId,
                detail => detail.Resolution.AvailableLocalItems == 3
                    && detail.Resolution.DownloadingItems == 0,
                timeoutMs: 10000);

            Assert.IsNotNull(submitted);
            Assert.AreEqual(2, submitted.SubmittedItems);
            Assert.AreEqual(3, synced.Resolution.AvailableLocalItems);
            Assert.AreEqual(0, synced.Resolution.DownloadingItems);
            Assert.AreEqual("AvailableLocal", synced.Items.Single(item => item.ProviderItemId == "missing-1").Status);
            Assert.AreEqual("AvailableLocal", synced.Items.Single(item => item.ProviderItemId == "missing-2").Status);

            await using var verifyScope = app.Services.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            var downloadedItems = await db.PlaylistItems
                .AsNoTracking()
                .Where(item => item.PlaylistId == playlistId && (item.ProviderItemId == "missing-1" || item.ProviderItemId == "missing-2"))
                .Select(item => new { item.ProviderItemId, item.CanonicalTrackId, item.Status })
                .ToListAsync();
            Assert.IsTrue(downloadedItems.All(item => item.CanonicalTrackId.HasValue));
            Assert.IsTrue(downloadedItems.All(item => item.Status == (int)PlaylistItemStatus.AvailableLocal));

            var workflows = await db.DownloadWorkflows
                .AsNoTracking()
                .Where(workflow => workflow.PlaylistItemId.HasValue)
                .ToListAsync();
            Assert.AreEqual(2, workflows.Count);
            Assert.IsTrue(workflows.All(workflow => workflow.Status == (int)DownloadWorkflowPersistenceStatus.Succeeded));
            Assert.IsTrue(workflows.All(workflow => !string.IsNullOrWhiteSpace(workflow.OutputPath)));
            Assert.IsTrue(await db.LocalMediaFiles.CountAsync(file => file.Availability == (int)LocalMediaAvailability.Available) >= 3);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task CancelActivePlaylistDownloads_MarksActiveItemsRetryableWithoutTouchingCompletedRows()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var playlistId = await SeedMissingPlaylistAsync(app);
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var submitted = await client.DownloadMissingPlaylistItemsAsync(playlistId);
            var cancelled = await client.CancelPlaylistDownloadsAsync(playlistId);
            var missing = await client.CancelPlaylistDownloadsAsync(Guid.NewGuid());

            Assert.IsNotNull(submitted);
            Assert.IsNotNull(cancelled);
            Assert.IsNull(missing);
            Assert.AreEqual(2, submitted.SubmittedItems);
            Assert.AreEqual(2, cancelled.CancelledItems + cancelled.FailedItems);
            Assert.AreEqual(2, cancelled.Resolution.FailedItems);
            Assert.AreEqual(1, cancelled.Resolution.AvailableLocalItems);
            Assert.AreEqual(1, cancelled.Resolution.RemovedItems);

            var cancelledItems = cancelled.Playlist.Items
                .Where(item => item.ProviderItemId is "missing-1" or "missing-2")
                .ToArray();
            Assert.IsTrue(cancelledItems.All(item => item.Status == "Failed"));
            Assert.AreEqual("AvailableLocal", cancelled.Playlist.Items.Single(item => item.ProviderItemId == "available-1").Status);
            Assert.AreEqual("RemovedFromSourcePlaylist", cancelled.Playlist.Items.Single(item => item.ProviderItemId == "removed-1").Status);

            await using var verifyScope = app.Services.CreateAsyncScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            var workflows = await db.DownloadWorkflows
                .AsNoTracking()
                .OrderBy(workflow => workflow.Id)
                .ToListAsync();
            Assert.AreEqual(2, workflows.Count);
            Assert.IsTrue(workflows.All(workflow =>
                workflow.Status == (int)DownloadWorkflowPersistenceStatus.Cancelled
                || workflow.Status == (int)DownloadWorkflowPersistenceStatus.Failed));
            Assert.IsTrue(workflows.All(workflow => workflow.ErrorCode is "cancelled_by_user" or "cancel_failed"));
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task PlayAvailablePlaylistItems_QueuesOnlyAvailableLocalItems()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var playlistId = await SeedMissingPlaylistAsync(app);
            Guid availableTrackId;
            Guid availableFileId;
            Guid missingItemId;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
                missingItemId = await db.PlaylistItems
                    .Where(item => item.PlaylistId == playlistId && item.ProviderItemId == "missing-1")
                    .Select(item => item.Id)
                    .SingleAsync();
                var available = await db.PlaylistItems
                    .Where(item => item.PlaylistId == playlistId && item.ProviderItemId == "available-1")
                    .Select(item => new
                    {
                        CanonicalTrackId = item.CanonicalTrackId!.Value,
                        LocalMediaFileId = item.CanonicalTrack!.LocalMediaFiles
                            .Where(file => file.Availability == (int)LocalMediaAvailability.Available)
                            .Select(file => file.Id)
                            .Single(),
                    })
                    .SingleAsync();
                availableTrackId = available.CanonicalTrackId;
                availableFileId = available.LocalMediaFileId;
            }

            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var playAvailable = await client.PlayAvailablePlaylistItemsAsync(playlistId);
            var playFromHere = await client.PlayPlaylistFromItemAsync(playlistId, missingItemId);
            var missingPlaylist = await client.PlayAvailablePlaylistItemsAsync(Guid.NewGuid());

            Assert.IsNotNull(playAvailable);
            Assert.IsNotNull(playFromHere);
            Assert.IsNull(missingPlaylist);
            Assert.AreEqual(1, playAvailable.Queue.Items.Count);
            Assert.AreEqual(0, playAvailable.Queue.CurrentIndex);
            Assert.AreEqual(availableTrackId, playAvailable.Queue.Items.Single().CanonicalTrackId);
            Assert.AreEqual(availableFileId, playAvailable.Queue.Items.Single().LocalMediaFileId);
            Assert.AreEqual(1, playFromHere.Queue.Items.Count);
            Assert.AreEqual(availableTrackId, playFromHere.Queue.Items.Single().CanonicalTrackId);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task SkipPlaylistItem_MarksItemSkippedWithoutDeletingRows()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var playlistId = await SeedMissingPlaylistAsync(app);
            Guid itemId;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
                itemId = await db.PlaylistItems
                    .Where(item => item.PlaylistId == playlistId && item.ProviderItemId == "missing-1")
                    .Select(item => item.Id)
                    .SingleAsync();
            }

            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var skipped = await client.SkipPlaylistItemAsync(playlistId, itemId);
            var missing = await client.SkipPlaylistItemAsync(playlistId, Guid.NewGuid());

            Assert.IsNotNull(skipped);
            Assert.IsNull(missing);
            Assert.AreEqual(4, skipped.Items.Count);
            Assert.AreEqual(1, skipped.Resolution.SkippedItems);
            Assert.AreEqual("Skipped", skipped.Items.Single(item => item.PlaylistItemId == itemId).Status);

            await using var verifyScope = app.Services.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            Assert.AreEqual(4, await verifyDb.PlaylistItems.CountAsync(item => item.PlaylistId == playlistId));
            Assert.AreEqual((int)PlaylistItemStatus.Skipped, await verifyDb.PlaylistItems
                .Where(item => item.Id == itemId)
                .Select(item => item.Status)
                .SingleAsync());
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task RetryPlaylistItem_SubmitsSkippedItemAndPersistsWorkflowLink()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var playlistId = await SeedMissingPlaylistAsync(app);
            Guid itemId;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
                itemId = await db.PlaylistItems
                    .Where(item => item.PlaylistId == playlistId && item.ProviderItemId == "missing-1")
                    .Select(item => item.Id)
                    .SingleAsync();
            }

            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            Assert.IsNotNull(await client.SkipPlaylistItemAsync(playlistId, itemId));
            var retried = await client.RetryPlaylistItemAsync(playlistId, itemId);
            var missing = await client.RetryPlaylistItemAsync(playlistId, Guid.NewGuid());

            Assert.IsNotNull(retried);
            Assert.IsNull(missing);
            Assert.AreEqual(1, retried.SubmittedItems);
            Assert.AreEqual(0, retried.FailedItems);
            Assert.AreEqual(1, retried.Submissions.Count);
            Assert.AreEqual(itemId, retried.Submissions.Single().PlaylistItemId);
            Assert.AreEqual("Downloading", retried.Playlist.Items.Single(item => item.PlaylistItemId == itemId).Status);

            await using var verifyScope = app.Services.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            var workflow = await verifyDb.DownloadWorkflows
                .AsNoTracking()
                .SingleAsync(workflow => workflow.PlaylistItemId == itemId);
            Assert.AreEqual(retried.Submissions.Single().EngineJobId, workflow.EngineJobId);
            Assert.AreEqual((int)PlaylistItemStatus.Downloading, await verifyDb.PlaylistItems
                .Where(item => item.Id == itemId)
                .Select(item => item.Status)
                .SingleAsync());
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    [TestMethod]
    public async Task ReviewLocalPlaylistItem_ApproveRejectAndRejectedCandidateStaysRejected()
    {
        var app = CreateApp(out var url, out var sessionToken, out var tempRoot);
        await app.StartAsync();
        try
        {
            var (playlistId, approvedItemId, rejectedItemId) = await SeedReviewPlaylistAsync(app);
            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);

            var approved = await client.ApprovePlaylistItemLocalMatchAsync(playlistId, approvedItemId);
            var rejected = await client.RejectPlaylistItemLocalMatchAsync(playlistId, rejectedItemId);
            var resolvedAgain = await client.ResolvePlaylistLocalAsync(playlistId);

            Assert.IsNotNull(approved);
            Assert.IsNotNull(rejected);
            Assert.IsNotNull(resolvedAgain);
            Assert.AreEqual("AvailableLocal", approved.Items.Single(item => item.PlaylistItemId == approvedItemId).Status);
            Assert.AreEqual("Unresolved", rejected.Items.Single(item => item.PlaylistItemId == rejectedItemId).Status);
            Assert.AreEqual("Unresolved", resolvedAgain.Playlist.Items.Single(item => item.PlaylistItemId == rejectedItemId).Status);
            Assert.AreEqual(0, resolvedAgain.ReviewItems);
            Assert.AreEqual(1, resolvedAgain.UnresolvedItems);

            await using var verifyScope = app.Services.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            var attempts = await verifyDb.ResolutionAttempts
                .AsNoTracking()
                .Where(attempt => attempt.PlaylistItemId == approvedItemId || attempt.PlaylistItemId == rejectedItemId)
                .ToListAsync();
            Assert.AreEqual(2, attempts.Count);
            Assert.AreEqual((int)ResolutionDecision.UserApproved, attempts.Single(attempt => attempt.PlaylistItemId == approvedItemId).Decision);
            Assert.AreEqual((int)ResolutionDecision.UserRejected, attempts.Single(attempt => attempt.PlaylistItemId == rejectedItemId).Decision);
            Assert.IsNull(await verifyDb.PlaylistItems
                .Where(item => item.Id == rejectedItemId)
                .Select(item => item.CanonicalTrackId)
                .SingleAsync());
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            DeleteTempRoot(tempRoot);
        }
    }

    private static async Task<Guid> SeedPlaylistAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ServerDatabaseMigrationService>().EnsureMigratedAsync();
        var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
        var playlistId = await new ExternalPlaylistSnapshotStore(db).UpsertAsync(new ExternalPlaylistSnapshotRecord(
            ExternalProvider.Spotify,
            "playlist-1",
            "Daily Mix",
            "https://example.test/playlist/1",
            1,
            new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero),
            PlaylistImportMode.Mirror,
            "Daily Mix",
            [
                new ExternalPlaylistItemSnapshot(
                    "item-1",
                    1,
                    "Track One",
                    "Artist One",
                    "Album One",
                    180000,
                    ExternalTrackId: "track-1",
                    Isrc: "USRC17607839",
                    ExternalUrl: "https://example.test/track/1",
                    ArtworkUrl: "https://example.test/art/1.jpg",
                    MusicBrainzRecordingId: "mbid-1",
                    Artists: ["Artist One", "Guest Artist"]),
                new ExternalPlaylistItemSnapshot("item-2", 2, "Track Two", "Artist Two", "Album Two", 181000),
                new ExternalPlaylistItemSnapshot("item-3", 3, "Track Three", "Artist Three", "Album Three", 182000),
            ],
            new ExternalAccountRecord(
                ExternalProvider.Spotify,
                "user-1",
                "Alice",
                "secret://spotify/1",
                new DateTimeOffset(2026, 9, 27, 11, 55, 0, TimeSpan.Zero))));

        db.CanonicalTracks.Add(new CanonicalTrackEntity
        {
            Id = Guid.NewGuid(),
            Artist = "Artist One",
            Title = "Track One",
            AlbumTitle = "Album One",
            DurationMs = 180000,
            NormalizedArtist = "artist one",
            NormalizedTitle = "track one",
        });
        var items = await db.PlaylistItems.ToDictionaryAsync(item => item.ProviderItemId);
        items["item-1"].CanonicalTrackId = db.CanonicalTracks.Local.Single().Id;
        items["item-1"].Status = (int)PlaylistItemStatus.AvailableLocal;
        items["item-2"].Status = (int)PlaylistItemStatus.Unresolved;
        items["item-3"].Status = (int)PlaylistItemStatus.RemovedFromSourcePlaylist;
        items["item-3"].RemovedAtUtc = new DateTimeOffset(2026, 9, 27, 12, 5, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();

        return playlistId;
    }

    private static async Task<Guid> SeedMissingPlaylistAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ServerDatabaseMigrationService>().EnsureMigratedAsync();
        var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
        var playlistId = await new ExternalPlaylistSnapshotStore(db).UpsertAsync(new ExternalPlaylistSnapshotRecord(
            ExternalProvider.YouTube,
            "playlist-download-missing",
            "Download Missing",
            "https://example.test/playlist/download-missing",
            1,
            new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero),
            PlaylistImportMode.Copy,
            "Download Missing",
            [
                new ExternalPlaylistItemSnapshot("missing-1", 1, "First Missing", "Artist One", "Album One", 180000),
                new ExternalPlaylistItemSnapshot("missing-2", 2, "Second Missing", "Artist Two", "Album Two", 181000),
                new ExternalPlaylistItemSnapshot("available-1", 3, "Available", "Artist Three", "Album Three", 182000),
                new ExternalPlaylistItemSnapshot("removed-1", 4, "Removed", "Artist Four", "Album Four", 183000),
            ],
            null));

        var items = await db.PlaylistItems.ToDictionaryAsync(item => item.ProviderItemId);
        db.CanonicalTracks.Add(new CanonicalTrackEntity
        {
            Id = Guid.NewGuid(),
            Artist = "Artist Three",
            Title = "Available",
            AlbumTitle = "Album Three",
            DurationMs = 182000,
            NormalizedArtist = "artist three",
            NormalizedTitle = "available",
            LocalMediaFiles =
            [
                new LocalMediaFileEntity
                {
                    Id = Guid.NewGuid(),
                    Path = "C:/Music/Artist Three/Available.flac",
                    Size = 1234,
                    LastWriteUtc = new DateTimeOffset(2026, 9, 28, 8, 55, 0, TimeSpan.Zero),
                    DurationMs = 182000,
                    Codec = "flac",
                    Bitrate = 900,
                    SampleRate = 44100,
                    BitDepth = 16,
                    Availability = (int)LocalMediaAvailability.Available,
                },
            ],
        });
        items["available-1"].CanonicalTrackId = db.CanonicalTracks.Local.Single().Id;
        items["available-1"].Status = (int)PlaylistItemStatus.AvailableLocal;
        items["removed-1"].Status = (int)PlaylistItemStatus.RemovedFromSourcePlaylist;
        items["removed-1"].RemovedAtUtc = new DateTimeOffset(2026, 9, 28, 9, 5, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();

        return playlistId;
    }

    private static async Task<Guid> SeedUnresolvedPlaylistWithLocalMatchAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ServerDatabaseMigrationService>().EnsureMigratedAsync();
        var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
        var playlistId = await new ExternalPlaylistSnapshotStore(db).UpsertAsync(new ExternalPlaylistSnapshotRecord(
            ExternalProvider.Spotify,
            "playlist-local-resolve",
            "Local Resolve",
            "https://example.test/playlist/local-resolve",
            1,
            new DateTimeOffset(2026, 9, 27, 13, 0, 0, TimeSpan.Zero),
            PlaylistImportMode.Copy,
            "Local Resolve",
            [
                new ExternalPlaylistItemSnapshot(
                    "item-local",
                    1,
                    "Local Track",
                    "Local Artist",
                    "Local Album",
                    200000,
                    Isrc: "USRC17607839"),
                new ExternalPlaylistItemSnapshot("item-missing", 2, "Missing Track", "Missing Artist", "Missing Album", 201000),
            ],
            null));

        db.CanonicalTracks.Add(new CanonicalTrackEntity
        {
            Id = Guid.NewGuid(),
            Artist = "Different Artist",
            Title = "Different Title",
            AlbumTitle = "Local Album",
            DurationMs = 200000,
            Isrc = "USRC17607839",
            NormalizedArtist = "different artist",
            NormalizedTitle = "different title",
            LocalMediaFiles =
            [
                new LocalMediaFileEntity
                {
                    Id = Guid.NewGuid(),
                    Path = "C:/Music/Local Artist/Local Track.mp3",
                    Size = 1234,
                    LastWriteUtc = new DateTimeOffset(2026, 9, 27, 12, 55, 0, TimeSpan.Zero),
                    DurationMs = 200000,
                    Codec = "mp3",
                    Bitrate = 320,
                    SampleRate = 44100,
                    BitDepth = 16,
                    Availability = (int)LocalMediaAvailability.Available,
                },
            ],
        });
        await db.SaveChangesAsync();

        return playlistId;
    }

    private static async Task<(Guid PlaylistId, Guid ApprovedItemId, Guid RejectedItemId)> SeedReviewPlaylistAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ServerDatabaseMigrationService>().EnsureMigratedAsync();
        var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
        var playlistId = await new ExternalPlaylistSnapshotStore(db).UpsertAsync(new ExternalPlaylistSnapshotRecord(
            ExternalProvider.Spotify,
            "playlist-review",
            "Review",
            "https://example.test/playlist/review",
            1,
            new DateTimeOffset(2026, 9, 28, 14, 0, 0, TimeSpan.Zero),
            PlaylistImportMode.Copy,
            "Review",
            [
                new ExternalPlaylistItemSnapshot("review-approve", 1, "Review Track", "Review Artist", "Review Album", 180000),
                new ExternalPlaylistItemSnapshot("review-reject", 2, "Review Track", "Review Artist", "Review Album", 180000),
            ],
            null));

        var trackId = Guid.NewGuid();
        db.CanonicalTracks.Add(new CanonicalTrackEntity
        {
            Id = trackId,
            Artist = "Review Artist",
            Title = "Review Track",
            AlbumTitle = "Review Album",
            DurationMs = 180000,
            NormalizedArtist = "review artist",
            NormalizedTitle = "review track",
            LocalMediaFiles =
            [
                new LocalMediaFileEntity
                {
                    Id = Guid.NewGuid(),
                    Path = "C:/Music/Review Artist/Review Track.flac",
                    Size = 1234,
                    LastWriteUtc = new DateTimeOffset(2026, 9, 28, 13, 55, 0, TimeSpan.Zero),
                    DurationMs = 180000,
                    Codec = "flac",
                    Bitrate = 900,
                    SampleRate = 44100,
                    BitDepth = 16,
                    Availability = (int)LocalMediaAvailability.Available,
                },
            ],
        });
        var items = await db.PlaylistItems.ToDictionaryAsync(item => item.ProviderItemId);
        items["review-approve"].CanonicalTrackId = trackId;
        items["review-approve"].Status = (int)PlaylistItemStatus.ReviewRequired;
        items["review-reject"].CanonicalTrackId = trackId;
        items["review-reject"].Status = (int)PlaylistItemStatus.ReviewRequired;
        await db.SaveChangesAsync();

        return (playlistId, items["review-approve"].Id, items["review-reject"].Id);
    }

    private static void SeedMockSoulseekFile(string tempRoot, string artist, string album, string filename)
    {
        var directory = Path.Combine(tempRoot, "music", artist, album);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, filename), new string('a', 4096));
    }

    private static async Task<PlaylistDetailDto> WaitForPlaylistSummaryAsync(
        SockseekApiClient client,
        Guid playlistId,
        Func<PlaylistDetailDto, bool> predicate,
        int timeoutMs = 5000)
    {
        using var timeout = new CancellationTokenSource(timeoutMs);
        PlaylistDetailDto? last = null;

        while (!timeout.IsCancellationRequested)
        {
            last = await client.GetPlaylistAsync(playlistId, timeout.Token);
            if (last != null && predicate(last))
                return last;

            try
            {
                await Task.Delay(100, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Assert.Fail($"Timed out waiting for playlist {playlistId} to reach expected summary. Last detail: {last}.");
        return null!;
    }

    private static WebApplication CreateApp(out string url, out string sessionToken, out string tempRoot)
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "Sockseek-playlist-test-" + Guid.NewGuid());
        var musicRoot = Path.Combine(tempRoot, "music");
        var outputRoot = Path.Combine(tempRoot, "downloads");
        Directory.CreateDirectory(musicRoot);
        Directory.CreateDirectory(outputRoot);
        url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        sessionToken = "playlist-test-token";
        return ServerHost.Build([], new ServerOptions
        {
            DatabasePath = Path.Combine(tempRoot, "sockseek.db"),
            Engine = new EngineSettings
            {
                MockFilesDir = musicRoot,
                MockFilesReadTags = false,
            },
            DefaultDownload = new DownloadSettings
            {
                Output =
                {
                    ParentDir = outputRoot,
                },
            },
            Profiles = ProfileCatalog.Empty,
            SecretStoreFactory = () => new InMemorySecretStore(),
            SessionToken = sessionToken,
        }, url);
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (!Directory.Exists(tempRoot))
            return;

        SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 19)
            {
                Thread.Sleep(250);
                SqliteConnection.ClearAllPools();
            }
            catch (UnauthorizedAccessException) when (attempt < 19)
            {
                Thread.Sleep(250);
                SqliteConnection.ClearAllPools();
            }
        }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
