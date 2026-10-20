namespace Sockseek.Server;

internal static class ServerDataDirectoryResolver
{
    public static string Resolve(string? configuredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
            return Path.GetFullPath(configuredDirectory);

        return ResolveDefault(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable("HOME"));
    }

    internal static string ResolveDefault(
        string? localApplicationData,
        string? xdgDataHome,
        string? userProfile,
        string? homeEnvironment)
    {
        if (!string.IsNullOrWhiteSpace(localApplicationData))
            return Path.GetFullPath(Path.Combine(localApplicationData, "Sockseek"));

        if (!string.IsNullOrWhiteSpace(xdgDataHome) && Path.IsPathRooted(xdgDataHome))
            return Path.GetFullPath(Path.Combine(xdgDataHome, "Sockseek"));

        string? home = !string.IsNullOrWhiteSpace(userProfile)
            ? userProfile
            : homeEnvironment;
        if (!string.IsNullOrWhiteSpace(home) && Path.IsPathRooted(home))
            return Path.GetFullPath(Path.Combine(home, ".local", "share", "Sockseek"));

        throw new InvalidOperationException(
            "Unable to resolve a local Sockseek data directory. Set SockseekServer__ConfigDir to an absolute writable path.");
    }
}
