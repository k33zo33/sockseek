namespace Sockseek.Server;

public sealed class EngineRuntimeHostedService : BackgroundService
{
    private readonly EngineSupervisor supervisor;
    private readonly PlaybackQueuePersistenceService queuePersistence;

    public EngineRuntimeHostedService(
        EngineSupervisor supervisor,
        PlaybackQueuePersistenceService queuePersistence)
    {
        this.supervisor = supervisor;
        this.queuePersistence = queuePersistence;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await queuePersistence.RestoreDefaultQueueAsync(stoppingToken);
        await supervisor.RunAsync(stoppingToken);
    }
}
