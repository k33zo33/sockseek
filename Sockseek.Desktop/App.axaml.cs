using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Sockseek.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public void ApplyThemePreference(DesktopThemePreference preference)
    {
        RequestedThemeVariant = preference switch
        {
            DesktopThemePreference.Light => ThemeVariant.Light,
            DesktopThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
