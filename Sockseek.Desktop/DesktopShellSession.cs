namespace Sockseek.Desktop;

public sealed class DesktopShellSession : IDesktopShellSession
{
    private readonly string? workspaceRoot;
    private readonly Func<string, DesktopDaemonLaunchRequest> launchRequestFactory;
    private bool disposed;

    public DesktopShellSession(
        DesktopDaemonSupervisor? supervisor = null,
        Func<DesktopDaemonHandshake, IDesktopEventHubConnection>? connectionFactory = null,
        IDesktopThemePreferenceStore? themePreferenceStore = null,
        string? workspaceRoot = null,
        Func<string, DesktopDaemonLaunchRequest>? launchRequestFactory = null)
    {
        Supervisor = supervisor ?? new DesktopDaemonSupervisor();
        RecoveryCoordinator = new DesktopBackendRecoveryCoordinator(Supervisor, connectionFactory);
        Shell = new ShellNavigationViewModel(Supervisor, themePreferenceStore);
        this.workspaceRoot = string.IsNullOrWhiteSpace(workspaceRoot) ? null : workspaceRoot.Trim();
        this.launchRequestFactory = launchRequestFactory ?? (root => DesktopDevelopmentDaemonLaunchRequestFactory.Create(root));
    }

    public DesktopDaemonSupervisor Supervisor { get; }

    public DesktopBackendRecoveryCoordinator RecoveryCoordinator { get; }

    public ShellNavigationViewModel Shell { get; }

    public bool CanStartDaemon => Supervisor.CanLaunch && workspaceRoot is not null;

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (Shell.BackendState == BackendConnectionState.Connected)
            return true;

        if (!CanStartDaemon || workspaceRoot is null)
            return false;

        return await Supervisor.TryLaunchAsync(launchRequestFactory(workspaceRoot), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        disposed = true;
        await RecoveryCoordinator.DisposeAsync();
        await Supervisor.DisposeAsync();
    }
}
