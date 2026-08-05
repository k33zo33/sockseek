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
            [ShellSection.Home] = CreatePage(ShellSection.Home, "Shell.Home.Title", "Shell.Home.Description", DesktopDesignTokens.Icon.Home),
            [ShellSection.Search] = CreatePage(ShellSection.Search, "Shell.Search.Title", "Shell.Search.Description", DesktopDesignTokens.Icon.Search),
            [ShellSection.Playlists] = CreatePage(ShellSection.Playlists, "Shell.Playlists.Title", "Shell.Playlists.Description", DesktopDesignTokens.Icon.Playlists),
            [ShellSection.Library] = CreatePage(ShellSection.Library, "Shell.Library.Title", "Shell.Library.Description", DesktopDesignTokens.Icon.Library),
            [ShellSection.Downloads] = CreatePage(ShellSection.Downloads, "Shell.Downloads.Title", "Shell.Downloads.Description", DesktopDesignTokens.Icon.Downloads),
            [ShellSection.Accounts] = CreatePage(ShellSection.Accounts, "Shell.Accounts.Title", "Shell.Accounts.Description", DesktopDesignTokens.Icon.Accounts),
            [ShellSection.Settings] = CreatePage(ShellSection.Settings, "Shell.Settings.Title", "Shell.Settings.Description", DesktopDesignTokens.Icon.Settings),
        };

    private static readonly IReadOnlyList<CommandPaletteItemViewModel> CommandPaletteItems =
        [
            CreateCommandPaletteItem("navigate-home", "Ctrl+1", "Shell.CommandPalette.NavigateHome", ShellSection.Home),
            CreateCommandPaletteItem("navigate-search", "Ctrl+L", "Shell.CommandPalette.NavigateSearch", ShellSection.Search),
            CreateCommandPaletteItem("navigate-playlists", "Ctrl+2", "Shell.CommandPalette.NavigatePlaylists", ShellSection.Playlists),
            CreateCommandPaletteItem("navigate-library", "Ctrl+3", "Shell.CommandPalette.NavigateLibrary", ShellSection.Library),
            CreateCommandPaletteItem("navigate-downloads", "Ctrl+4", "Shell.CommandPalette.NavigateDownloads", ShellSection.Downloads),
            CreateCommandPaletteItem("navigate-accounts", "Ctrl+5", "Shell.CommandPalette.NavigateAccounts", ShellSection.Accounts),
            CreateCommandPaletteItem("navigate-settings", "Ctrl+,", "Shell.CommandPalette.NavigateSettings", ShellSection.Settings)
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
        => Pages.TryGetValue(section, out var page)
            ? page.Title
            : section.ToString();

    private static ShellPageViewModel CreatePage(
        ShellSection section,
        string titleResourceKey,
        string descriptionResourceKey,
        string iconToken)
        => new(
            section,
            DesktopStringResources.Get(titleResourceKey),
            DesktopStringResources.Get(descriptionResourceKey),
            titleResourceKey,
            descriptionResourceKey,
            iconToken);

    private static CommandPaletteItemViewModel CreateCommandPaletteItem(
        string id,
        string shortcut,
        string titleResourceKey,
        ShellSection section)
        => new(id, DesktopStringResources.Get(titleResourceKey), shortcut, titleResourceKey, section);

    private static BackendStatusBannerViewModel CreateBanner(BackendConnectionState state)
        => state switch
        {
            BackendConnectionState.Starting => CreateBanner(state, "Shell.Backend.Starting.Title", "Shell.Backend.Starting.Message", true, DesktopDesignTokens.Surface.BannerInfo, DesktopDesignTokens.Icon.BannerInfo),
            BackendConnectionState.Connected => CreateBanner(state, "Shell.Backend.Connected.Title", "Shell.Backend.Connected.Message", false, DesktopDesignTokens.Surface.BannerSuccess, DesktopDesignTokens.Icon.BannerSuccess),
            BackendConnectionState.Restarting => CreateBanner(state, "Shell.Backend.Restarting.Title", "Shell.Backend.Restarting.Message", true, DesktopDesignTokens.Surface.BannerWarning, DesktopDesignTokens.Icon.BannerWarning),
            BackendConnectionState.Disconnected => CreateBanner(state, "Shell.Backend.Disconnected.Title", "Shell.Backend.Disconnected.Message", true, DesktopDesignTokens.Surface.BannerDanger, DesktopDesignTokens.Icon.BannerDanger),
            BackendConnectionState.Unauthorized => CreateBanner(state, "Shell.Backend.Unauthorized.Title", "Shell.Backend.Unauthorized.Message", true, DesktopDesignTokens.Surface.BannerDanger, DesktopDesignTokens.Icon.BannerDanger),
            _ => CreateBanner(state, "Shell.Backend.Unknown.Title", "Shell.Backend.Unknown.Message", true, DesktopDesignTokens.Surface.BannerWarning, DesktopDesignTokens.Icon.BannerWarning),
        };

    private static BackendStatusBannerViewModel CreateBanner(
        BackendConnectionState state,
        string titleResourceKey,
        string messageResourceKey,
        bool isVisible,
        string surfaceToken,
        string iconToken)
        => new(
            state,
            DesktopStringResources.Get(titleResourceKey),
            DesktopStringResources.Get(messageResourceKey),
            isVisible,
            surfaceToken,
            iconToken,
            titleResourceKey,
            messageResourceKey);

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
