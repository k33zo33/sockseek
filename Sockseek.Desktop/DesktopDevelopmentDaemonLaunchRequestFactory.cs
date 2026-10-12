namespace Sockseek.Desktop;

public static class DesktopDevelopmentDaemonLaunchRequestFactory
{
    public const string HandshakeStdoutEnvironmentVariable = "SOCKSEEK_DESKTOP_HANDSHAKE_STDOUT";
    public const string ConfigDirEnvironmentVariable = "SOCKSEEK_DESKTOP_CONFIG_DIR";
    public const string DefaultDotnetExecutable = "dotnet";
    public const string DefaultServerProjectPath = "Sockseek.Server/Sockseek.Server.csproj";
    public const string DefaultListenUrl = "http://127.0.0.1:0";
    public const string DefaultArguments = "run --project " + DefaultServerProjectPath + " --no-launch-profile --urls " + DefaultListenUrl;

    public static DesktopDaemonLaunchRequest Create(string workspaceRoot, string? dotnetExecutable = null, string? configDir = null)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));

        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [HandshakeStdoutEnvironmentVariable] = "1",
        };
        var resolvedConfigDir = !string.IsNullOrWhiteSpace(configDir)
            ? configDir
            : Environment.GetEnvironmentVariable(ConfigDirEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(resolvedConfigDir))
            environment["SockseekServer__ConfigDir"] = Path.GetFullPath(resolvedConfigDir);

        return new DesktopDaemonLaunchRequest(
            dotnetExecutable?.Trim() is { Length: > 0 } executable ? executable : DefaultDotnetExecutable,
            DefaultArguments,
            workspaceRoot,
            environment);
    }
}
