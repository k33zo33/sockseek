namespace Sockseek.Desktop;

public sealed class DesktopShellWindowViewModel : ObservableObject
{
    public DesktopShellWindowViewModel(DesktopShellSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Session.Shell.PropertyChanged += HandleShellPropertyChanged;
    }

    public DesktopShellSession Session { get; }

    public string TitleResourceKey { get; } = "Shell.Window.Title";

    public string Title { get; } = DesktopStringResources.Get("Shell.Window.Title");

    public string SurfaceToken { get; } = DesktopDesignTokens.Surface.AppCanvas;

    public string ChromeSpacingToken { get; } = DesktopDesignTokens.Spacing.ShellChrome;

    public ShellNavigationViewModel Shell => Session.Shell;

    public BackendStatusBannerViewModel StatusBanner => Session.Shell.StatusBanner;

    public PlayerBarPlaceholderViewModel PlayerBar => Session.Shell.PlayerBar;

    public ShellPageViewModel CurrentPage => Session.Shell.CurrentPage;

    public DesktopThemePreference CurrentTheme => Session.Shell.CurrentTheme;

    public string WindowTitle => $"{Title} — {CurrentPage.Title}";

    public bool CanCopyDiagnostics => StatusBanner.CanCopyDiagnostics;

    public string? CopyDiagnosticsLabel => StatusBanner.CopyDiagnosticsLabel;

    public string? CopyDiagnosticsLabelResourceKey => StatusBanner.CopyDiagnosticsLabelResourceKey;

    public string? CopyDiagnosticsHint => StatusBanner.CopyDiagnosticsHint;

    public string? CopyDiagnosticsHintResourceKey => StatusBanner.CopyDiagnosticsHintResourceKey;

    public bool CanStartDaemon => Session.CanStartDaemon && Shell.BackendState is BackendConnectionState.Disconnected or BackendConnectionState.Unauthorized;

    public string StartDaemonLabel => DesktopStringResources.Get("Shell.Backend.Action.StartDaemon.Label");

    public string StartDaemonLabelResourceKey { get; } = "Shell.Backend.Action.StartDaemon.Label";

    public string StartDaemonHint => DesktopStringResources.Get("Shell.Backend.Action.StartDaemon.Hint");

    public string StartDaemonHintResourceKey { get; } = "Shell.Backend.Action.StartDaemon.Hint";

    public string DiagnosticsText => CreateDiagnosticsText();

    public Task<bool> TryStartDaemonAsync(CancellationToken cancellationToken = default)
        => CanStartDaemon
            ? Session.StartAsync(cancellationToken)
            : Task.FromResult(false);

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
        switch (eventArgs.PropertyName)
        {
            case nameof(ShellNavigationViewModel.CurrentPage):
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(DiagnosticsText));
                break;
            case nameof(ShellNavigationViewModel.CurrentTheme):
                OnPropertyChanged(nameof(CurrentTheme));
                OnPropertyChanged(nameof(DiagnosticsText));
                break;
            case nameof(ShellNavigationViewModel.StatusBanner):
                OnPropertyChanged(nameof(StatusBanner));
                OnPropertyChanged(nameof(CanCopyDiagnostics));
                OnPropertyChanged(nameof(CopyDiagnosticsLabel));
                OnPropertyChanged(nameof(CopyDiagnosticsLabelResourceKey));
                OnPropertyChanged(nameof(CopyDiagnosticsHint));
                OnPropertyChanged(nameof(CopyDiagnosticsHintResourceKey));
                OnPropertyChanged(nameof(DiagnosticsText));
                break;
            case nameof(ShellNavigationViewModel.BackendState):
                OnPropertyChanged(nameof(CanStartDaemon));
                OnPropertyChanged(nameof(DiagnosticsText));
                break;
            case nameof(ShellNavigationViewModel.CurrentHandshake):
                OnPropertyChanged(nameof(DiagnosticsText));
                break;
        }
    }
}
