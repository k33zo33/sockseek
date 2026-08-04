namespace Sockseek.Desktop;

public sealed class ShellNavigationViewModel
{
    private static readonly IReadOnlyDictionary<string, ShellSection> ShortcutMap =
        new Dictionary<string, ShellSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ctrl+1"] = ShellSection.Home,
            ["Ctrl+L"] = ShellSection.Search,
            ["Ctrl+2"] = ShellSection.Playlists,
            ["Ctrl+3"] = ShellSection.Library,
            ["Ctrl+4"] = ShellSection.Downloads,
            ["Ctrl+5"] = ShellSection.Accounts,
            ["Ctrl+,"] = ShellSection.Settings,
        };

    public ShellNavigationViewModel()
    {
        Items = Enum.GetValues<ShellSection>()
            .Select(section => new ShellNavigationItem(section, GetDisplayName(section)))
            .ToArray();
        CurrentSection = ShellSection.Home;
    }

    public IReadOnlyList<ShellNavigationItem> Items { get; }

    public ShellSection CurrentSection { get; private set; }

    public void NavigateTo(ShellSection section)
        => CurrentSection = section;

    public bool TryHandleShortcut(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return false;

        if (!ShortcutMap.TryGetValue(shortcut.Trim(), out var section))
            return false;

        NavigateTo(section);
        return true;
    }

    private static string GetDisplayName(ShellSection section)
        => section switch
        {
            ShellSection.Home => "Home",
            ShellSection.Search => "Search",
            ShellSection.Playlists => "Playlists",
            ShellSection.Library => "Library",
            ShellSection.Downloads => "Downloads",
            ShellSection.Accounts => "Accounts",
            ShellSection.Settings => "Settings",
            _ => section.ToString(),
        };
}

public sealed record ShellNavigationItem(ShellSection Section, string DisplayName);
