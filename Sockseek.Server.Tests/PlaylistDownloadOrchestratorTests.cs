using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Soulseek;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class PlaylistDownloadOrchestratorTests
{
    [TestMethod]
    public async Task DownloadMissingAsync_ForwardsProfileNameToSoulseekGateway()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SockseekDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new SockseekDbContext(options))
            await setup.Database.EnsureCreatedAsync();

        Guid playlistId;
        await using (var seed = new SockseekDbContext(options))
        {
            playlistId = await new ExternalPlaylistSnapshotStore(seed).UpsertAsync(new ExternalPlaylistSnapshotRecord(
                ExternalProvider.Spotify,
                "playlist-profile",
                "Profile Playlist",
                "https://example.test/playlist/profile",
                1,
                new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero),
                PlaylistImportMode.Copy,
                "Profile Playlist",
                [new ExternalPlaylistItemSnapshot("item-profile", 1, "Profile Track", "Profile Artist", "Profile Album", 180000)],
                null));
        }

        var gateway = new CapturingSoulseekEngineGateway();
        await using (var db = new SockseekDbContext(options))
        {
            var orchestrator = new PlaylistDownloadOrchestrator(db, gateway);

            var result = await orchestrator.DownloadMissingAsync(playlistId, "  lossless  ");

            Assert.IsTrue(result.PlaylistFound);
            Assert.AreEqual(1, result.SubmittedItems);
        }

        Assert.AreEqual(1, gateway.TrackDownloadRequests.Count);
        var submitted = gateway.TrackDownloadRequests.Single();
        Assert.AreEqual("Profile Artist", submitted.Request.Artist);
        Assert.AreEqual("Profile Track", submitted.Request.Title);
        Assert.AreEqual("Profile Album", submitted.Request.Album);
        Assert.AreEqual("lossless", submitted.Options.ProfileName);
        Assert.IsNull(submitted.Options.OutputParentDir);
    }

    private sealed class CapturingSoulseekEngineGateway : ISoulseekEngineGateway
    {
        public List<(TrackSearchRequest Request, DownloadOptions Options)> TrackDownloadRequests { get; } = [];

        public Task<SearchHandle> StartTrackSearchAsync(TrackSearchRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new SearchHandle(Guid.NewGuid(), Guid.NewGuid()));

        public Task<SearchHandle> StartAlbumSearchAsync(AlbumSearchRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new SearchHandle(Guid.NewGuid(), Guid.NewGuid()));

        public Task<DownloadHandle> StartTrackDownloadAsync(
            TrackSearchRequest request,
            DownloadOptions options,
            CancellationToken cancellationToken)
        {
            TrackDownloadRequests.Add((request, options));
            return Task.FromResult(new DownloadHandle(Guid.NewGuid(), Guid.NewGuid()));
        }

        public Task<DownloadHandle> StartDownloadAsync(
            CandidateReference candidate,
            DownloadOptions options,
            CancellationToken cancellationToken)
            => Task.FromResult(new DownloadHandle(Guid.NewGuid(), Guid.NewGuid()));

        public Task CancelJobAsync(Guid engineJobId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<bool> TryNextCandidateAsync(Guid engineJobId, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task<JobSnapshot?> GetJobAsync(Guid engineJobId, CancellationToken cancellationToken)
            => Task.FromResult<JobSnapshot?>(null);

        public Task<DownloadJobResultSnapshot?> GetDownloadResultAsync(Guid engineJobId, CancellationToken cancellationToken)
            => Task.FromResult<DownloadJobResultSnapshot?>(null);

        public IAsyncEnumerable<EngineEventEnvelope> SubscribeAsync(Guid workflowId, CancellationToken cancellationToken)
            => EmptyEventsAsync();

        private static async IAsyncEnumerable<EngineEventEnvelope> EmptyEventsAsync()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
