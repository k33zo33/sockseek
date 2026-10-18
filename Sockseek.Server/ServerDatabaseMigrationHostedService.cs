using Microsoft.Extensions.Options;

namespace Sockseek.Server;

public sealed class ServerDatabaseMigrationHostedService(
    ServerDatabaseMigrationService migrationService,
    IOptions<ServerOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => ShouldMigrate(options.Value)
            ? migrationService.EnsureMigratedAsync(cancellationToken)
            : Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static bool ShouldMigrate(ServerOptions serverOptions)
        => !string.IsNullOrWhiteSpace(serverOptions.DatabasePath)
            || !string.IsNullOrWhiteSpace(serverOptions.ConfigDir)
            || !AppDomain.CurrentDomain.GetAssemblies()
                .Any(assembly => assembly.GetName().Name?.EndsWith(".Tests", StringComparison.Ordinal) == true);
}
