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
                break;
            case nameof(ShellNavigationViewModel.CurrentTheme):
                OnPropertyChanged(nameof(CurrentTheme));
                break;
            case nameof(ShellNavigationViewModel.StatusBanner):
                OnPropertyChanged(nameof(StatusBanner));
                break;
        }
    }
}
