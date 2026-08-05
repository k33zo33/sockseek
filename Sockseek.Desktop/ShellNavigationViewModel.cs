namespace Sockseek.Desktop;

public sealed class ShellNavigationViewModel
{
    private readonly DesktopDaemonSupervisor? supervisor;
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
            [ShellSection.Home] = new(ShellSection.Home, "Home", "Backend status, recent activity, and onboarding live here."),
            [ShellSection.Search] = new(ShellSection.Search, "Search", "Track and album search UI will appear here."),
            [ShellSection.Playlists] = new(ShellSection.Playlists, "Playlists", "Imported playlists and resolution progress will appear here."),
            [ShellSection.Library] = new(ShellSection.Library, "Library", "Local library browsing and scans will appear here."),
            [ShellSection.Downloads] = new(ShellSection.Downloads, "Downloads", "Active and completed download workflows will appear here."),
            [ShellSection.Accounts] = new(ShellSection.Accounts, "Accounts", "Provider connections and authorization status will appear here."),
            [ShellSection.Settings] = new(ShellSection.Settings, "Settings", "Theme, daemon, and library preferences will appear here."),
        };

    public ShellNavigationViewModel(DesktopDaemonSupervisor? supervisor = null)
    {
        this.supervisor = supervisor;
        Items = Enum.GetValues<ShellSection>()
            .Select(section => new ShellNavigationItem(section, GetDisplayName(section)))
            .ToArray();
        PlayerBar = new PlayerBarPlaceholderViewModel();

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

    public BackendConnectionState BackendState { get; private set; }

    public BackendStatusBannerViewModel StatusBanner { get; private set; } = CreateBanner(BackendConnectionState.Starting);

    public DesktopDaemonHandshake? CurrentHandshake { get; private set; }

    public void NavigateTo(ShellSection section)
    {
        CurrentSection = section;
        CurrentPage = Pages[section];
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

        if (!ShortcutMap.TryGetValue(shortcut.Trim(), out var section))
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
            BackendConnectionState.Starting => new(state, "Starting local daemon", "Sockseek is launching the backend and waiting for a secure session.", true),
            BackendConnectionState.Connected => new(state, "Connected", "Local daemon is ready.", false),
            BackendConnectionState.Restarting => new(state, "Restarting local daemon", "The backend is restarting. UI actions will resume automatically.", true),
            BackendConnectionState.Disconnected => new(state, "Backend disconnected", "Sockseek cannot currently reach the local daemon.", true),
            BackendConnectionState.Unauthorized => new(state, "Session expired", "The desktop shell needs a fresh local session handshake.", true),
            _ => new(state, "Backend status unknown", "Sockseek cannot determine backend state yet.", true),
        };
}

public sealed record ShellNavigationItem(ShellSection Section, string DisplayName);
