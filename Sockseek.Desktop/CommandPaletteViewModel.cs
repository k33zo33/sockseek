namespace Sockseek.Desktop;

public sealed class CommandPaletteViewModel
{
    public CommandPaletteViewModel(IReadOnlyList<CommandPaletteItemViewModel> items)
    {
        Items = items;
    }

    public bool IsOpen { get; private set; }

    public string Title { get; } = "Command palette";

    public string TitleResourceKey { get; } = "Shell.CommandPalette.Title";

    public string Placeholder { get; } = "Jump to a section or action";

    public string PlaceholderResourceKey { get; } = "Shell.CommandPalette.Placeholder";

    public IReadOnlyList<CommandPaletteItemViewModel> Items { get; }

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    public void Toggle() => IsOpen = !IsOpen;
}

public sealed record CommandPaletteItemViewModel(
    string Id,
    string Title,
    string Shortcut,
    string TitleResourceKey,
    ShellSection? TargetSection = null);
