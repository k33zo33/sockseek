namespace Sockseek.Desktop;

public static class DesktopSettingsPaths
{
    public static string GetThemePreferenceFilePath()
    {
        var baseDirectory = ResolveLocalDataDirectory(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable("HOME"));

        return Path.Combine(baseDirectory, "Sockseek", "Desktop", "theme-preference.json");
    }

    internal static string ResolveLocalDataDirectory(
        string? localApplicationData,
        string? xdgDataHome,
        string? userProfile,
        string? homeEnvironment)
    {
        if (!string.IsNullOrWhiteSpace(localApplicationData))
            return Path.GetFullPath(localApplicationData);

        if (!string.IsNullOrWhiteSpace(xdgDataHome) && Path.IsPathRooted(xdgDataHome))
            return Path.GetFullPath(xdgDataHome);

        string? home = !string.IsNullOrWhiteSpace(userProfile)
            ? userProfile
            : homeEnvironment;
        if (!string.IsNullOrWhiteSpace(home) && Path.IsPathRooted(home))
            return Path.GetFullPath(Path.Combine(home, ".local", "share"));

        throw new InvalidOperationException(
            "Unable to resolve a local Desktop settings directory. Set XDG_DATA_HOME to an absolute writable path.");
    }
}
