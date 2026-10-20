using Microsoft.Extensions.DependencyInjection;
using Sockseek.Api;
using Sockseek.Application.Common;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.SoulseekSharing;

namespace Sockseek.Server;

public sealed class SoulseekShareEndpointService(
    IServiceScopeFactory scopeFactory,
    ServerDatabaseMigrationService databaseMigration,
    SoulseekShareService shareService)
{
    public async Task<SoulseekSharingSnapshotDto> GetAsync(CancellationToken cancellationToken)
    {
        await databaseMigration.EnsureMigratedAsync(cancellationToken);
        return await CreateSnapshotAsync(cancellationToken);
    }

    public async Task<SoulseekSharingSnapshotDto> SaveAsync(
        Guid? id,
        SaveSoulseekShareRootRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await databaseMigration.EnsureMigratedAsync(cancellationToken);
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var store = CreateStore(scope.ServiceProvider);
            await store.SaveAsync(id, request.Path, request.VirtualName, request.Enabled, cancellationToken);
        }

        shareService.Invalidate();
        await shareService.RebuildAsync(cancellationToken);
        return await CreateSnapshotAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        await databaseMigration.EnsureMigratedAsync(cancellationToken);
        bool removed;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            removed = await CreateStore(scope.ServiceProvider).RemoveAsync(id, cancellationToken);
        }

        if (removed)
        {
            shareService.Invalidate();
            await shareService.RebuildAsync(cancellationToken);
        }
        return removed;
    }

    public async Task<SoulseekSharingSnapshotDto> RebuildAsync(CancellationToken cancellationToken)
    {
        await databaseMigration.EnsureMigratedAsync(cancellationToken);
        await shareService.RebuildAsync(cancellationToken);
        return await CreateSnapshotAsync(cancellationToken);
    }

    private async Task<SoulseekSharingSnapshotDto> CreateSnapshotAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SoulseekShareRootRecord> configuredRoots;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            configuredRoots = await CreateStore(scope.ServiceProvider).ListAsync(cancellationToken);
        }

        var state = shareService.GetState();
        var active = state.Snapshot;
        var statuses = state.BuildResult.Roots.ToDictionary(status => status.Id);
        return new SoulseekSharingSnapshotDto(
            active.Revision,
            active.DirectoryCount,
            active.FileCount,
            configuredRoots.Select(root =>
            {
                statuses.TryGetValue(root.Id, out var status);
                return new SoulseekShareRootDto(
                    root.Id,
                    root.Path,
                    root.VirtualName,
                    root.Enabled,
                    status?.Included ?? false,
                    status is null ? "not_indexed" : status.ErrorCode ?? "ready",
                    status?.DirectoryCount ?? 0,
                    status?.FileCount ?? 0,
                    root.CreatedAtUtc,
                    root.UpdatedAtUtc);
            }).ToArray());
    }

    private static SoulseekShareRootStore CreateStore(IServiceProvider services)
        => new(
            services.GetRequiredService<SockseekDbContext>(),
            services.GetRequiredService<IClock>());
}
