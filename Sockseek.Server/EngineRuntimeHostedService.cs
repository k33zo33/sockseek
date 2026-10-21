using Microsoft.Extensions.Options;

namespace Sockseek.Server;

public sealed class EngineRuntimeHostedService : BackgroundService
{
    private readonly EngineSupervisor supervisor;
    private readonly PlaybackQueuePersistenceService queuePersistence;
    private readonly PlaylistWorkflowRecoveryService playlistWorkflowRecovery;
    private readonly SoulseekShareService shareService;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly IOptions<ServerOptions> options;

    public EngineRuntimeHostedService(
        EngineSupervisor supervisor,
        PlaybackQueuePersistenceService queuePersistence,
        PlaylistWorkflowRecoveryService playlistWorkflowRecovery,
        SoulseekShareService shareService,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ServerOptions> options)
    {
        this.supervisor = supervisor;
        this.queuePersistence = queuePersistence;
        this.playlistWorkflowRecovery = playlistWorkflowRecovery;
        this.shareService = shareService;
        this.serviceScopeFactory = serviceScopeFactory;
        this.options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await playlistWorkflowRecovery.MarkInterruptedPlaylistWorkflowsAsync(stoppingToken);
        await queuePersistence.RestoreDefaultQueueAsync(stoppingToken);
        if (ServerDatabaseMigrationHostedService.ShouldMigrate(options.Value))
            await shareService.RebuildAsync(stoppingToken);
        await using (var scope = serviceScopeFactory.CreateAsyncScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<SoulseekAccountService>()
                .RestoreStoredCredentialsAsync(stoppingToken);
        }
        await supervisor.RunAsync(stoppingToken);
    }
}
