using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sockseek.Domain.Playlists;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Server;

public sealed class PlaylistWorkflowRecoveryService(IServiceScopeFactory scopeFactory)
{
    public async Task<int> MarkInterruptedPlaylistWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ServerDatabaseMigrationService>().EnsureMigratedAsync(cancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<SockseekDbContext>();
        var workflows = await db.DownloadWorkflows
            .Include(workflow => workflow.PlaylistItem)
            .Where(workflow => workflow.PlaylistItemId.HasValue
                && (workflow.Status == (int)DownloadWorkflowPersistenceStatus.Searching
                    || workflow.Status == (int)DownloadWorkflowPersistenceStatus.Downloading))
            .ToListAsync(cancellationToken);

        if (workflows.Count == 0)
            return 0;

        var now = DateTimeOffset.UtcNow;
        foreach (var workflow in workflows)
        {
            workflow.Status = (int)DownloadWorkflowPersistenceStatus.Failed;
            workflow.ErrorCode = "interrupted_by_restart";
            workflow.UpdatedAtUtc = now;

            var item = workflow.PlaylistItem;
            if (item is null || item.RemovedAtUtc.HasValue || item.CanonicalTrackId.HasValue)
                continue;

            var status = ToPlaylistItemStatus(item.Status);
            if (status is PlaylistItemStatus.Searching or PlaylistItemStatus.CandidateFound or PlaylistItemStatus.Downloading)
                item.Status = (int)PlaylistItemStatus.Failed;
        }

        await db.SaveChangesAsync(cancellationToken);
        return workflows.Count;
    }

    private static PlaylistItemStatus ToPlaylistItemStatus(int status)
        => Enum.IsDefined(typeof(PlaylistItemStatus), status)
            ? (PlaylistItemStatus)status
            : PlaylistItemStatus.Unresolved;
}
