using System.Text.Json;

namespace Sockseek.Desktop;

public sealed class DesktopFileThemePreferenceStore(string filePath) : IDesktopThemePreferenceStore
{
    public DesktopThemePreference Load()
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return DesktopThemePreference.System;

        try
        {
            var contents = File.ReadAllText(filePath);
            var payload = JsonSerializer.Deserialize<DesktopThemePreferencePayload>(contents);
            return Enum.TryParse<DesktopThemePreference>(payload?.Theme, ignoreCase: true, out var preference)
                ? preference
                : DesktopThemePreference.System;
        }
        catch (IOException)
        {
            return DesktopThemePreference.System;
        }
        catch (UnauthorizedAccessException)
        {
            return DesktopThemePreference.System;
        }
        catch (JsonException)
        {
            return DesktopThemePreference.System;
        }
    }

    public void Save(DesktopThemePreference preference)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var payload = JsonSerializer.Serialize(new DesktopThemePreferencePayload(preference.ToString()));
        File.WriteAllText(filePath, payload);
    }

    private sealed record DesktopThemePreferencePayload(string Theme);
}
