namespace Sockseek.Desktop;

public static class DesktopDaemonLaunchRequestFactory
{
    public const string PackagedDaemonDirectoryName = "daemon";
    public const string ServerExecutableBaseName = "Sockseek.Server";

    public static DesktopDaemonLaunchRequest Create(
        string workspaceRoot,
        string applicationBaseDirectory,
        string? configDir = null)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
        if (string.IsNullOrWhiteSpace(applicationBaseDirectory))
            throw new ArgumentException("Application base directory is required.", nameof(applicationBaseDirectory));

        var baseDirectory = Path.GetFullPath(applicationBaseDirectory);
        var daemonDirectory = Path.Combine(baseDirectory, PackagedDaemonDirectoryName);
        var executableName = OperatingSystem.IsWindows()
            ? ServerExecutableBaseName + ".exe"
            : ServerExecutableBaseName;
        var executablePath = Path.Combine(daemonDirectory, executableName);

        return File.Exists(executablePath)
            ? CreatePackagedRequest(executablePath, daemonDirectory, configDir)
            : DesktopDevelopmentDaemonLaunchRequestFactory.Create(workspaceRoot, configDir: configDir);
    }

    private static DesktopDaemonLaunchRequest CreatePackagedRequest(
        string executablePath,
        string daemonDirectory,
        string? configuredConfigDir)
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [DesktopDevelopmentDaemonLaunchRequestFactory.HandshakeStdoutEnvironmentVariable] = "1",
        };
        var configDir = !string.IsNullOrWhiteSpace(configuredConfigDir)
            ? configuredConfigDir
            : Environment.GetEnvironmentVariable(DesktopDevelopmentDaemonLaunchRequestFactory.ConfigDirEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configDir))
            environment["SockseekServer__ConfigDir"] = Path.GetFullPath(configDir);

        return new DesktopDaemonLaunchRequest(
            executablePath,
            "--urls " + DesktopDevelopmentDaemonLaunchRequestFactory.DefaultListenUrl,
            daemonDirectory,
            environment);
    }
}
