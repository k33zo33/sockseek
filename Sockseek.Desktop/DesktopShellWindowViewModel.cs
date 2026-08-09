namespace Sockseek.Desktop;

public sealed class DesktopShellWindowViewModel : ObservableObject, IDisposable
{
    private bool isStartingDaemon;
    private bool disposed;

    public DesktopShellWindowViewModel(IDesktopShellSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Session.Shell.PropertyChanged += HandleShellPropertyChanged;
        Session.Shell.CommandPalette.PropertyChanged += HandleCommandPalettePropertyChanged;
    }

    public IDesktopShellSession Session { get; }

    public string TitleResourceKey { get; } = "Shell.Window.Title";

    public string Title { get; } = DesktopStringResources.Get("Shell.Window.Title");

    public string SurfaceToken { get; } = DesktopDesignTokens.Surface.AppCanvas;

    public string ChromeSpacingToken { get; } = DesktopDesignTokens.Spacing.ShellChrome;

    public ShellNavigationViewModel Shell => Session.Shell;

    public BackendStatusBannerViewModel StatusBanner => Session.Shell.StatusBanner;

    public string BackendBannerTitle => StatusBanner.Title;

    public string BackendBannerTitleResourceKey => StatusBanner.TitleResourceKey;

    public string BackendBannerMessage => StatusBanner.Message;

    public string BackendBannerMessageResourceKey => StatusBanner.MessageResourceKey;

    public bool IsBackendBannerVisible => StatusBanner.IsVisible;

    public string BackendBannerSurfaceToken => StatusBanner.SurfaceToken;

    public string BackendBannerIconToken => StatusBanner.IconToken;

    public string BackendBannerIconAccessibilityLabel => StatusBanner.IconAccessibilityLabel;

    public string BackendBannerIconAccessibilityLabelResourceKey => StatusBanner.IconAccessibilityLabelResourceKey;

    public IReadOnlyList<ShellNavigationItem> NavigationItems => Session.Shell.Items;

    public PlayerBarPlaceholderViewModel PlayerBar => Session.Shell.PlayerBar;

    public CommandPaletteViewModel CommandPalette => Session.Shell.CommandPalette;

    public ShellSection CurrentSection => Session.Shell.CurrentSection;

    public ShellPageViewModel CurrentPage => Session.Shell.CurrentPage;

    public string CurrentPageTitle => CurrentPage.Title;

    public string CurrentPageTitleResourceKey => CurrentPage.TitleResourceKey;

    public string CurrentPageDescription => CurrentPage.Description;

    public string CurrentPageDescriptionResourceKey => CurrentPage.DescriptionResourceKey;

    public string CurrentPageIconToken => CurrentPage.IconToken;

    public DesktopThemePreference CurrentTheme => Session.Shell.CurrentTheme;

    public BackendConnectionState BackendState => Shell.BackendState;

    public DesktopDaemonHandshake? CurrentHandshake => Shell.CurrentHandshake;

    public bool HasCurrentHandshake => CurrentHandshake is not null;

    public string WindowTitle => $"{Title} — {CurrentPage.Title}";

    public bool IsCommandPaletteOpen => Shell.CommandPalette.IsOpen;

    public bool CanCopyDiagnostics => StatusBanner.CanCopyDiagnostics;

    public string? CopyDiagnosticsLabel => StatusBanner.CopyDiagnosticsLabel;

    public string? CopyDiagnosticsLabelResourceKey => StatusBanner.CopyDiagnosticsLabelResourceKey;

    public string? CopyDiagnosticsHint => StatusBanner.CopyDiagnosticsHint;

    public string? CopyDiagnosticsHintResourceKey => StatusBanner.CopyDiagnosticsHintResourceKey;

    public bool CanStartDaemon => !IsStartingDaemon && Session.CanStartDaemon && Shell.BackendState is BackendConnectionState.Disconnected or BackendConnectionState.Unauthorized;

    public string StartDaemonLabel => DesktopStringResources.Get("Shell.Backend.Action.StartDaemon.Label");

    public string StartDaemonLabelResourceKey { get; } = "Shell.Backend.Action.StartDaemon.Label";

    public string StartDaemonHint => DesktopStringResources.Get("Shell.Backend.Action.StartDaemon.Hint");

    public string StartDaemonHintResourceKey { get; } = "Shell.Backend.Action.StartDaemon.Hint";

    public bool IsStartingDaemon
    {
        get => isStartingDaemon;
        private set
        {
            if (!SetProperty(ref isStartingDaemon, value))
                return;

            OnPropertyChanged(nameof(CanStartDaemon));
        }
    }

    public string DiagnosticsText => CreateDiagnosticsText();

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Session.Shell.PropertyChanged -= HandleShellPropertyChanged;
        Session.Shell.CommandPalette.PropertyChanged -= HandleCommandPalettePropertyChanged;
    }

    public void OpenCommandPalette() => Shell.OpenCommandPalette();

    public void CloseCommandPalette() => Shell.CloseCommandPalette();

    public bool TryHandleShortcut(string shortcut) => Shell.TryHandleShortcut(shortcut);

    public bool TryExecuteCommandPaletteItem(string itemId) => Shell.TryExecuteCommandPaletteItem(itemId);

    public async Task<bool> TryStartDaemonAsync(CancellationToken cancellationToken = default)
    {
        if (!CanStartDaemon)
            return false;

        IsStartingDaemon = true;
        try
        {
            return await Session.StartAsync(cancellationToken);
        }
        finally
        {
            IsStartingDaemon = false;
        }
    }

    public string? TryGetCopyDiagnosticsText()
        => CanCopyDiagnostics
            ? CreateDiagnosticsText()
            : null;

    public DesktopShellDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        => new(
            WindowTitle,
            CurrentPage.Title,
            CurrentTheme.ToString(),
            Shell.BackendState.ToString(),
            StatusBanner.Title,
            Shell.CurrentHandshake is not null,
            Shell.CurrentHandshake?.BaseUrl);

    public string CreateDiagnosticsText()
        => CreateDiagnosticsSnapshot().ToDisplayText();

    private void HandleShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (disposed)
            return;

        switch (eventArgs.PropertyName)
        {
            case nameof(ShellNavigationViewModel.CurrentSection):
                OnPropertyChanged(nameof(CurrentSection));
                break;
            case nameof(ShellNavigationViewModel.CurrentPage):
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(CurrentPageTitle));
                OnPropertyChanged(nameof(CurrentPageTitleResourceKey));
                OnPropertyChanged(nameof(CurrentPageDescription));
                OnPropertyChanged(nameof(CurrentPageDescriptionResourceKey));
                OnPropertyChanged(nameof(CurrentPageIconToken));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(DiagnosticsText));
                break;
            case nameof(ShellNavigationViewModel.CurrentTheme):
                OnPropertyChanged(nameof(CurrentTheme));
                OnPropertyChanged(nameof(DiagnosticsText));
                break;
            case nameof(ShellNavigationViewModel.StatusBanner):
                OnPropertyChanged(nameof(StatusBanner));
                OnPropertyChanged(nameof(BackendBannerTitle));
                OnPropertyChanged(nameof(BackendBannerTitleResourceKey));
                OnPropertyChanged(nameof(BackendBannerMessage));
                OnPropertyChanged(nameof(BackendBannerMessageResourceKey));
                OnPropertyChanged(nameof(IsBackendBannerVisible));
                OnPropertyChanged(nameof(BackendBannerSurfaceToken));
                OnPropertyChanged(nameof(BackendBannerIconToken));
                OnPropertyChanged(nameof(BackendBannerIconAccessibilityLabel));
                OnPropertyChanged(nameof(BackendBannerIconAccessibilityLabelResourceKey));
                OnPropertyChanged(nameof(CanCopyDiagnostics));
                OnPropertyChanged(nameof(CopyDiagnosticsLabel));
                OnPropertyChanged(nameof(CopyDiagnosticsLabelResourceKey));
                OnPropertyChanged(nameof(CopyDiagnosticsHint));
                OnPropertyChanged(nameof(CopyDiagnosticsHintResourceKey));
                OnPropertyChanged(nameof(DiagnosticsText));
                break;
            case nameof(ShellNavigationViewModel.BackendState):
                OnPropertyChanged(nameof(BackendState));
                OnPropertyChanged(nameof(CanStartDaemon));
                OnPropertyChanged(nameof(DiagnosticsText));
                break;
            case nameof(ShellNavigationViewModel.CurrentHandshake):
                OnPropertyChanged(nameof(CurrentHandshake));
                OnPropertyChanged(nameof(HasCurrentHandshake));
                OnPropertyChanged(nameof(DiagnosticsText));
                break;
        }
    }

    private void HandleCommandPalettePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (disposed)
            return;

        if (eventArgs.PropertyName == nameof(CommandPaletteViewModel.IsOpen))
            OnPropertyChanged(nameof(IsCommandPaletteOpen));
    }
}
