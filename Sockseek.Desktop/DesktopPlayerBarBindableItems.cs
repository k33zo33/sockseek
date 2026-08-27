using System.Windows.Input;

namespace Sockseek.Desktop;

public sealed class DesktopPlayerBarActionViewModel
{
    public DesktopPlayerBarActionViewModel(
        string iconGlyph,
        string iconToken,
        string accessibilityLabel,
        string accessibilityLabelResourceKey,
        string hint,
        string hintResourceKey,
        bool isEnabled,
        Action execute)
    {
        IconGlyph = string.IsNullOrWhiteSpace(iconGlyph)
            ? throw new ArgumentException("Value cannot be null or whitespace.", nameof(iconGlyph))
            : iconGlyph;
        IconToken = iconToken ?? throw new ArgumentNullException(nameof(iconToken));
        AccessibilityLabel = accessibilityLabel ?? throw new ArgumentNullException(nameof(accessibilityLabel));
        AccessibilityLabelResourceKey = accessibilityLabelResourceKey ?? throw new ArgumentNullException(nameof(accessibilityLabelResourceKey));
        Hint = hint ?? throw new ArgumentNullException(nameof(hint));
        HintResourceKey = hintResourceKey ?? throw new ArgumentNullException(nameof(hintResourceKey));
        IsEnabled = isEnabled;
        Command = new DesktopCommand(execute ?? throw new ArgumentNullException(nameof(execute)));
    }

    public string IconGlyph { get; }

    public string IconToken { get; }

    public string AccessibilityLabel { get; }

    public string AccessibilityLabelResourceKey { get; }

    public string Hint { get; }

    public string HintResourceKey { get; }

    public bool IsEnabled { get; }

    public ICommand Command { get; }
}
