namespace Sockseek.Desktop;

public sealed class ShellNavigationViewModel
{
    private readonly DesktopDaemonSupervisor? supervisor;
    private readonly IDesktopThemePreferenceStore themePreferenceStore;
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

    private static readonly IReadOnlyDictionary<ShellSection, ShellPageViewModel> Pages =
        new Dictionary<ShellSection, ShellPageViewModel>
        {
            [ShellSection.Home] = new(ShellSection.Home, "Home", "Backend status, recent activity, and onboarding live here.", "Shell.Home.Title", "Shell.Home.Description", DesktopDesignTokens.Icon.Home),
            [ShellSection.Search] = new(ShellSection.Search, "Search", "Track and album search UI will appear here.", "Shell.Search.Title", "Shell.Search.Description", DesktopDesignTokens.Icon.Search),
            [ShellSection.Playlists] = new(ShellSection.Playlists, "Playlists", "Imported playlists and resolution progress will appear here.", "Shell.Playlists.Title", "Shell.Playlists.Description", DesktopDesignTokens.Icon.Playlists),
            [ShellSection.Library] = new(ShellSection.Library, "Library", "Local library browsing and scans will appear here.", "Shell.Library.Title", "Shell.Library.Description", DesktopDesignTokens.Icon.Library),
            [ShellSection.Downloads] = new(ShellSection.Downloads, "Downloads", "Active and completed download workflows will appear here.", "Shell.Downloads.Title", "Shell.Downloads.Description", DesktopDesignTokens.Icon.Downloads),
            [ShellSection.Accounts] = new(ShellSection.Accounts, "Accounts", "Provider connections and authorization status will appear here.", "Shell.Accounts.Title", "Shell.Accounts.Description", DesktopDesignTokens.Icon.Accounts),
            [ShellSection.Settings] = new(ShellSection.Settings, "Settings", "Theme, daemon, and library preferences will appear here.", "Shell.Settings.Title", "Shell.Settings.Description", DesktopDesignTokens.Icon.Settings),
        };

    private static readonly IReadOnlyList<CommandPaletteItemViewModel> CommandPaletteItems =
        [
            new("navigate-home", "Go to Home", "Ctrl+1", "Shell.CommandPalette.NavigateHome", ShellSection.Home),
            new("navigate-search", "Go to Search", "Ctrl+L", "Shell.CommandPalette.NavigateSearch", ShellSection.Search),
            new("navigate-playlists", "Go to Playlists", "Ctrl+2", "Shell.CommandPalette.NavigatePlaylists", ShellSection.Playlists),
            new("navigate-library", "Go to Library", "Ctrl+3", "Shell.CommandPalette.NavigateLibrary", ShellSection.Library),
            new("navigate-downloads", "Go to Downloads", "Ctrl+4", "Shell.CommandPalette.NavigateDownloads", ShellSection.Downloads),
            new("navigate-accounts", "Go to Accounts", "Ctrl+5", "Shell.CommandPalette.NavigateAccounts", ShellSection.Accounts),
            new("navigate-settings", "Go to Settings", "Ctrl+,", "Shell.CommandPalette.NavigateSettings", ShellSection.Settings)
        ];

    public ShellNavigationViewModel(DesktopDaemonSupervisor? supervisor = null, IDesktopThemePreferenceStore? themePreferenceStore = null)
    {
        this.supervisor = supervisor;
        this.themePreferenceStore = themePreferenceStore ?? new InMemoryDesktopThemePreferenceStore();
        Items = Enum.GetValues<ShellSection>()
            .Select(section => new ShellNavigationItem(section, GetDisplayName(section), GetIconToken(section)))
            .ToArray();
        PlayerBar = new PlayerBarPlaceholderViewModel();
        CurrentTheme = this.themePreferenceStore.Load();
        CommandPalette = new CommandPaletteViewModel(CommandPaletteItems);

        if (this.supervisor is not null)
        {
            ApplySupervisorSnapshot(this.supervisor.CurrentSnapshot);
            this.supervisor.SnapshotChanged += HandleSupervisorSnapshotChanged;
        }
        else
        {
            SetBackendState(BackendConnectionState.Starting);
        }

        NavigateTo(ShellSection.Home);
    }

    public IReadOnlyList<ShellNavigationItem> Items { get; }

    public ShellSection CurrentSection { get; private set; }

    public ShellPageViewModel CurrentPage { get; private set; } = Pages[ShellSection.Home];

    public PlayerBarPlaceholderViewModel PlayerBar { get; }

    public DesktopThemePreference CurrentTheme { get; private set; }

    public CommandPaletteViewModel CommandPalette { get; }

    public BackendConnectionState BackendState { get; private set; }

    public BackendStatusBannerViewModel StatusBanner { get; private set; } = CreateBanner(BackendConnectionState.Starting);

    public DesktopDaemonHandshake? CurrentHandshake { get; private set; }

    public void NavigateTo(ShellSection section)
    {
        CurrentSection = section;
        CurrentPage = Pages[section];
    }

    public void SetTheme(DesktopThemePreference preference)
    {
        CurrentTheme = preference;
        themePreferenceStore.Save(preference);
    }

    public void OpenCommandPalette() => CommandPalette.Open();

    public void CloseCommandPalette() => CommandPalette.Close();

    public bool TryExecuteCommandPaletteItem(string itemId)
    {
        if (!CommandPalette.TryGetItem(itemId, out var item) || item is null)
            return false;

        if (item.TargetSection is { } targetSection)
            NavigateTo(targetSection);

        CloseCommandPalette();
        return true;
    }

    public void SetBackendState(BackendConnectionState state)
    {
        BackendState = state;
        if (state != BackendConnectionState.Connected)
            CurrentHandshake = null;
        StatusBanner = CreateBanner(state);
    }

    public bool TryHandleShortcut(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return false;

        var normalizedShortcut = shortcut.Trim();
        if (string.Equals(normalizedShortcut, "Ctrl+K", StringComparison.OrdinalIgnoreCase))
        {
            OpenCommandPalette();
            return true;
        }

        if (!ShortcutMap.TryGetValue(normalizedShortcut, out var section))
            return false;

        NavigateTo(section);
        return true;
    }

    private void HandleSupervisorSnapshotChanged(object? sender, DesktopDaemonSupervisorSnapshot snapshot)
        => ApplySupervisorSnapshot(snapshot);

    private void ApplySupervisorSnapshot(DesktopDaemonSupervisorSnapshot snapshot)
    {
        CurrentHandshake = snapshot.Handshake;
        SetBackendState(snapshot.State);
        CurrentHandshake = snapshot.Handshake;
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

    private static BackendStatusBannerViewModel CreateBanner(BackendConnectionState state)
        => state switch
        {
            BackendConnectionState.Starting => new(state, "Starting local daemon", "Sockseek is launching the backend and waiting for a secure session.", true, DesktopDesignTokens.Surface.BannerInfo, DesktopDesignTokens.Icon.BannerInfo),
            BackendConnectionState.Connected => new(state, "Connected", "Local daemon is ready.", false, DesktopDesignTokens.Surface.BannerSuccess, DesktopDesignTokens.Icon.BannerSuccess),
            BackendConnectionState.Restarting => new(state, "Restarting local daemon", "The backend is restarting. UI actions will resume automatically.", true, DesktopDesignTokens.Surface.BannerWarning, DesktopDesignTokens.Icon.BannerWarning),
            BackendConnectionState.Disconnected => new(state, "Backend disconnected", "Sockseek cannot currently reach the local daemon.", true, DesktopDesignTokens.Surface.BannerDanger, DesktopDesignTokens.Icon.BannerDanger),
            BackendConnectionState.Unauthorized => new(state, "Session expired", "The desktop shell needs a fresh local session handshake.", true, DesktopDesignTokens.Surface.BannerDanger, DesktopDesignTokens.Icon.BannerDanger),
            _ => new(state, "Backend status unknown", "Sockseek cannot determine backend state yet.", true, DesktopDesignTokens.Surface.BannerWarning, DesktopDesignTokens.Icon.BannerWarning),
        };

    private static string GetIconToken(ShellSection section)
        => section switch
        {
            ShellSection.Home => DesktopDesignTokens.Icon.Home,
            ShellSection.Search => DesktopDesignTokens.Icon.Search,
            ShellSection.Playlists => DesktopDesignTokens.Icon.Playlists,
            ShellSection.Library => DesktopDesignTokens.Icon.Library,
            ShellSection.Downloads => DesktopDesignTokens.Icon.Downloads,
            ShellSection.Accounts => DesktopDesignTokens.Icon.Accounts,
            ShellSection.Settings => DesktopDesignTokens.Icon.Settings,
            _ => DesktopDesignTokens.Icon.Home,
        };
}

public sealed record ShellNavigationItem(ShellSection Section, string DisplayName, string IconToken)
{
    public string SurfaceToken { get; } = DesktopDesignTokens.Surface.Sidebar;

    public string TypographyToken { get; } = DesktopDesignTokens.Typography.Body;
}
