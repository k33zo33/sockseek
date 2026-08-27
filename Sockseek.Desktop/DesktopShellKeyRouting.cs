using Avalonia.Input;

namespace Sockseek.Desktop;

internal static class DesktopShellKeyRouting
{
    public static bool TryHandleKeyGesture(DesktopShellWindowViewModel? viewModel, Key key, KeyModifiers modifiers)
    {
        var shouldClosePalette = key == Key.Escape;
        var shortcut = modifiers.HasFlag(KeyModifiers.Control) && TryMapShortcut(key, out var mappedShortcut)
            ? mappedShortcut
            : null;

        return TryHandleShellInput(viewModel, shortcut, shouldClosePalette);
    }

    public static bool TryHandleShellInput(DesktopShellWindowViewModel? viewModel, string? shortcut, bool shouldClosePalette)
    {
        if (viewModel is null)
            return false;

        if (shouldClosePalette && viewModel.IsCommandPaletteOpen)
        {
            viewModel.CloseCommandPalette();
            return true;
        }

        return !string.IsNullOrWhiteSpace(shortcut)
            && viewModel.TryHandleShortcut(shortcut);
    }

    public static bool TryMapShortcut(Key key, out string shortcut)
    {
        shortcut = key switch
        {
            Key.D1 => "Ctrl+1",
            Key.L => "Ctrl+L",
            Key.D2 => "Ctrl+2",
            Key.D3 => "Ctrl+3",
            Key.D4 => "Ctrl+4",
            Key.D5 => "Ctrl+5",
            Key.OemComma => "Ctrl+,",
            Key.K => "Ctrl+K",
            _ => string.Empty,
        };

        return shortcut.Length > 0;
    }
}
