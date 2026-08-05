namespace Sockseek.Desktop;

public interface IDesktopThemePreferenceStore
{
    DesktopThemePreference Load();
    void Save(DesktopThemePreference preference);
}

public sealed class InMemoryDesktopThemePreferenceStore(DesktopThemePreference initialPreference = DesktopThemePreference.System) : IDesktopThemePreferenceStore
{
    private DesktopThemePreference preference = initialPreference;

    public DesktopThemePreference Load() => preference;

    public void Save(DesktopThemePreference preference)
        => this.preference = preference;
}
