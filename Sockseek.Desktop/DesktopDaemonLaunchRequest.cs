namespace Sockseek.Desktop;

public sealed record DesktopDaemonLaunchRequest(
    string FileName,
    string Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> EnvironmentVariables);
