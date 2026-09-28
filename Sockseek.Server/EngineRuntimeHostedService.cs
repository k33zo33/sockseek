namespace Sockseek.Server;

public sealed class EngineRuntimeHostedService : BackgroundService
{
    private readonly EngineSupervisor supervisor;
    private readonly PlaybackQueuePersistenceService queuePersistence;
    private readonly PlaylistWorkflowRecoveryService playlistWorkflowRecovery;

    public EngineRuntimeHostedService(
        EngineSupervisor supervisor,
        PlaybackQueuePersistenceService queuePersistence,
        PlaylistWorkflowRecoveryService playlistWorkflowRecovery)
    {
        this.supervisor = supervisor;
        this.queuePersistence = queuePersistence;
        this.playlistWorkflowRecovery = playlistWorkflowRecovery;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await playlistWorkflowRecovery.MarkInterruptedPlaylistWorkflowsAsync(stoppingToken);
        await queuePersistence.RestoreDefaultQueueAsync(stoppingToken);
        await supervisor.RunAsync(stoppingToken);
    }
}
