namespace Sockseek.Desktop;

public sealed class PlayerBarPlaceholderViewModel
{
    public string Title { get; } = "Nothing playing";

    public string Artist { get; } = "Choose a local track or completed download";

    public bool CanGoPrevious { get; } = false;

    public bool CanPlayPause { get; } = false;

    public bool CanGoNext { get; } = false;

    public string QueueSummary { get; } = "Queue unavailable until playback coordinator is connected";

    public string SurfaceToken { get; } = DesktopDesignTokens.Surface.PlayerBar;

    public string TitleTypographyToken { get; } = DesktopDesignTokens.Typography.PlayerTitle;

    public string ArtistTypographyToken { get; } = DesktopDesignTokens.Typography.PlayerSubtitle;

    public string QueueIconToken { get; } = DesktopDesignTokens.Icon.PlayerQueue;

    public string PaddingToken { get; } = DesktopDesignTokens.Spacing.PlayerBarPadding;
}
