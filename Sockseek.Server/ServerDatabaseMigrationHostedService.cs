using Microsoft.Extensions.Options;

namespace Sockseek.Server;

public sealed class ServerDatabaseMigrationHostedService(
    ServerDatabaseMigrationService migrationService,
    IOptions<ServerOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => UsesImplicitDatabasePathInTestProcess(options.Value)
            ? Task.CompletedTask
            : migrationService.EnsureMigratedAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool UsesImplicitDatabasePathInTestProcess(ServerOptions serverOptions)
        => string.IsNullOrWhiteSpace(serverOptions.DatabasePath)
            && string.IsNullOrWhiteSpace(serverOptions.ConfigDir)
            && AppDomain.CurrentDomain.GetAssemblies()
                .Any(assembly => assembly.GetName().Name?.EndsWith(".Tests", StringComparison.Ordinal) == true);
}
