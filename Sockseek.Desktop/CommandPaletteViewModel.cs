namespace Sockseek.Desktop;

public sealed class CommandPaletteViewModel
{
    private readonly Dictionary<string, CommandPaletteItemViewModel> itemsById;

    public CommandPaletteViewModel(IReadOnlyList<CommandPaletteItemViewModel> items)
    {
        Items = items;
        itemsById = items.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsOpen { get; private set; }

    public string TitleResourceKey { get; } = "Shell.CommandPalette.Title";

    public string Title { get; } = DesktopStringResources.Get("Shell.CommandPalette.Title");

    public string PlaceholderResourceKey { get; } = "Shell.CommandPalette.Placeholder";

    public string Placeholder { get; } = DesktopStringResources.Get("Shell.CommandPalette.Placeholder");

    public string SurfaceToken { get; } = DesktopDesignTokens.Surface.CommandPalette;

    public string TitleTypographyToken { get; } = DesktopDesignTokens.Typography.CommandPaletteTitle;

    public string ItemTypographyToken { get; } = DesktopDesignTokens.Typography.CommandPaletteItem;

    public string PaddingToken { get; } = DesktopDesignTokens.Spacing.CommandPalettePadding;

    public IReadOnlyList<CommandPaletteItemViewModel> Items { get; }

    public bool TryGetItem(string id, out CommandPaletteItemViewModel? item)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            item = null;
            return false;
        }

        return itemsById.TryGetValue(id.Trim(), out item);
    }

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
