using Avalonia.Input;

namespace Sockseek.Desktop;

internal static class DesktopShellKeyRouting
{
    public static bool TryHandleKeyGesture(
        DesktopShellWindowViewModel? viewModel,
        Key key,
        KeyModifiers modifiers,
        bool isTextInputFocused = false)
    {
        var shouldClosePalette = key == Key.Escape;
        var playerInput = modifiers == KeyModifiers.None && TryMapPlayerInput(key, isTextInputFocused, out var mappedPlayerInput)
            ? (DesktopPlayerInput?)mappedPlayerInput
            : null;
        var shortcut = modifiers.HasFlag(KeyModifiers.Control) && TryMapShortcut(key, out var mappedShortcut)
            ? mappedShortcut
            : null;

        return TryHandleShellInput(viewModel, shortcut, shouldClosePalette, playerInput);
    }

    public static bool TryHandleShellInput(
        DesktopShellWindowViewModel? viewModel,
        string? shortcut,
        bool shouldClosePalette,
        DesktopPlayerInput? playerInput = null)
    {
        if (viewModel is null)
            return false;

        if (shouldClosePalette && viewModel.IsCommandPaletteOpen)
        {
            viewModel.CloseCommandPalette();
            return true;
        }

        if (playerInput is DesktopPlayerInput input)
            return viewModel.TryHandlePlayerInput(input);

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

    public static bool TryMapPlayerInput(Key key, bool isTextInputFocused, out DesktopPlayerInput input)
    {
        input = key switch
        {
            Key.Space when !isTextInputFocused => DesktopPlayerInput.TogglePlayPause,
            Key.MediaPlayPause => DesktopPlayerInput.TogglePlayPause,
            Key.MediaPreviousTrack => DesktopPlayerInput.Previous,
            Key.MediaNextTrack => DesktopPlayerInput.Next,
            Key.VolumeMute => DesktopPlayerInput.ToggleMute,
            _ => default,
        };

        return key is Key.MediaPlayPause
            or Key.MediaPreviousTrack
            or Key.MediaNextTrack
            or Key.VolumeMute
            || (key == Key.Space && !isTextInputFocused);
    }
}
