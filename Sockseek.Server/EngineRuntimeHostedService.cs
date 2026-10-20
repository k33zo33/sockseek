using Microsoft.Extensions.Options;

namespace Sockseek.Server;

public sealed class EngineRuntimeHostedService : BackgroundService
{
    private readonly EngineSupervisor supervisor;
    private readonly PlaybackQueuePersistenceService queuePersistence;
    private readonly PlaylistWorkflowRecoveryService playlistWorkflowRecovery;
    private readonly SoulseekShareService shareService;
    private readonly IOptions<ServerOptions> options;

    public EngineRuntimeHostedService(
        EngineSupervisor supervisor,
        PlaybackQueuePersistenceService queuePersistence,
        PlaylistWorkflowRecoveryService playlistWorkflowRecovery,
        SoulseekShareService shareService,
        IOptions<ServerOptions> options)
    {
        this.supervisor = supervisor;
        this.queuePersistence = queuePersistence;
        this.playlistWorkflowRecovery = playlistWorkflowRecovery;
        this.shareService = shareService;
        this.options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await playlistWorkflowRecovery.MarkInterruptedPlaylistWorkflowsAsync(stoppingToken);
        await queuePersistence.RestoreDefaultQueueAsync(stoppingToken);
        if (ServerDatabaseMigrationHostedService.ShouldMigrate(options.Value))
            await shareService.RebuildAsync(stoppingToken);
        await supervisor.RunAsync(stoppingToken);
    }
}
