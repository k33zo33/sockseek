namespace Sockseek.Desktop;

public sealed class PlayerBarPlaceholderViewModel
{
    public string TitleResourceKey { get; } = "Shell.PlayerBar.Title";

    public string Title { get; } = DesktopStringResources.Get("Shell.PlayerBar.Title");

    public string ArtistResourceKey { get; } = "Shell.PlayerBar.Artist";

    public string Artist { get; } = DesktopStringResources.Get("Shell.PlayerBar.Artist");

    public bool CanGoPrevious { get; } = false;

    public bool CanPlayPause { get; } = false;

    public bool CanGoNext { get; } = false;

    public string QueueSummaryResourceKey { get; } = "Shell.PlayerBar.QueueSummary";

    public string QueueSummary { get; } = DesktopStringResources.Get("Shell.PlayerBar.QueueSummary");

    public string SurfaceToken { get; } = DesktopDesignTokens.Surface.PlayerBar;

    public string TitleTypographyToken { get; } = DesktopDesignTokens.Typography.PlayerTitle;

    public string ArtistTypographyToken { get; } = DesktopDesignTokens.Typography.PlayerSubtitle;

    public string QueueIconToken { get; } = DesktopDesignTokens.Icon.PlayerQueue;

    public string PaddingToken { get; } = DesktopDesignTokens.Spacing.PlayerBarPadding;
}
