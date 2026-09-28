using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class PlaylistWorkflowRecoveryServiceTests
{
    [TestMethod]
    public async Task MarkInterruptedPlaylistWorkflowsAsync_MarksActivePlaylistWorkflowRetryable()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "Sockseek-playlist-recovery-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempRoot);
        var databasePath = Path.Combine(tempRoot, "sockseek.db");
        await using var services = CreateServices(databasePath);
        try
        {
            Guid itemId;
            Guid workflowRowId;
            await services.GetRequiredService<ServerDatabaseMigrationService>().EnsureMigratedAsync();
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
                var playlistId = await new ExternalPlaylistSnapshotStore(db).UpsertAsync(new ExternalPlaylistSnapshotRecord(
                    ExternalProvider.YouTube,
                    "playlist-recovery",
                    "Recovery",
                    "https://example.test/playlist/recovery",
                    1,
                    new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero),
                    PlaylistImportMode.Copy,
                    "Recovery",
                    [new ExternalPlaylistItemSnapshot("item-recovery", 1, "Track", "Artist", "Album", 180000)],
                    null));

                var item = await db.PlaylistItems.SingleAsync(entity => entity.PlaylistId == playlistId);
                itemId = item.Id;
                item.Status = (int)PlaylistItemStatus.Searching;
                workflowRowId = Guid.NewGuid();
                db.DownloadWorkflows.Add(new DownloadWorkflowEntity
                {
                    Id = workflowRowId,
                    WorkflowId = Guid.NewGuid(),
                    EngineJobId = Guid.NewGuid(),
                    PlaylistItemId = itemId,
                    Status = (int)DownloadWorkflowPersistenceStatus.Searching,
                    CandidateJson = "{}",
                    CreatedAtUtc = new DateTimeOffset(2026, 9, 28, 10, 1, 0, TimeSpan.Zero),
                    UpdatedAtUtc = new DateTimeOffset(2026, 9, 28, 10, 1, 0, TimeSpan.Zero),
                });
                await db.SaveChangesAsync();
            }

            var recovered = await services.GetRequiredService<PlaylistWorkflowRecoveryService>()
                .MarkInterruptedPlaylistWorkflowsAsync();

            Assert.AreEqual(1, recovered);
            await using var verifyScope = services.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SockseekDbContext>();
            var workflow = await verifyDb.DownloadWorkflows.AsNoTracking().SingleAsync(entity => entity.Id == workflowRowId);
            Assert.AreEqual((int)DownloadWorkflowPersistenceStatus.Failed, workflow.Status);
            Assert.AreEqual("interrupted_by_restart", workflow.ErrorCode);
            Assert.AreEqual((int)PlaylistItemStatus.Failed, await verifyDb.PlaylistItems
                .Where(item => item.Id == itemId)
                .Select(item => item.Status)
                .SingleAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTempRoot(tempRoot);
        }
    }

    private static ServiceProvider CreateServices(string databasePath)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<ServerOptions>>(Options.Create(new ServerOptions
        {
            DatabasePath = databasePath,
        }));
        services.AddSingleton<ServerDatabaseMigrationService>();
        services.AddDbContext<SockseekDbContext>(db => db.UseSqlite($"Data Source={databasePath}"));
        services.AddSingleton<PlaylistWorkflowRecoveryService>();
        return services.BuildServiceProvider();
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (!Directory.Exists(tempRoot))
            return;

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
}
