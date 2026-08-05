namespace Sockseek.Desktop;

public sealed class DesktopShellWindowViewModel(DesktopShellSession session)
{
    public DesktopShellSession Session { get; } = session ?? throw new ArgumentNullException(nameof(session));

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
}
