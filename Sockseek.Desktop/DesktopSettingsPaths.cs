namespace Sockseek.Desktop;

public static class DesktopSettingsPaths
{
    public static string GetThemePreferenceFilePath()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
            baseDirectory = AppContext.BaseDirectory;

        return Path.Combine(baseDirectory, "Sockseek", "Desktop", "theme-preference.json");
    }
}
