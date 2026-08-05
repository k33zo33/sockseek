namespace Sockseek.Desktop;

public sealed class PlayerBarPlaceholderViewModel
{
    public string TitleResourceKey { get; } = "Shell.PlayerBar.Title";

    public string Title { get; } = DesktopStringResources.Get("Shell.PlayerBar.Title");

    public string ArtistResourceKey { get; } = "Shell.PlayerBar.Artist";

    public string Artist { get; } = DesktopStringResources.Get("Shell.PlayerBar.Artist");

    public bool CanGoPrevious { get; } = false;

    public string PreviousIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.Previous.IconLabel";

    public string PreviousIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.Previous.IconLabel");

    public bool CanPlayPause { get; } = false;

    public string PlayPauseIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.PlayPause.IconLabel";

    public string PlayPauseIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.PlayPause.IconLabel");

    public string PlayPauseHintResourceKey { get; } = "Shell.PlayerBar.PlayPause.Hint";

    public string PlayPauseHint { get; } = DesktopStringResources.Get("Shell.PlayerBar.PlayPause.Hint");

    public bool CanGoNext { get; } = false;

    public string NextIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.Next.IconLabel";

    public string NextIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.Next.IconLabel");

    public string QueueSummaryResourceKey { get; } = "Shell.PlayerBar.QueueSummary";

    public string QueueSummary { get; } = DesktopStringResources.Get("Shell.PlayerBar.QueueSummary");

    public string QueueIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.Queue.IconLabel";

    public string QueueIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.Queue.IconLabel");

    public string QueueHintResourceKey { get; } = "Shell.PlayerBar.Queue.Hint";

    public string QueueHint { get; } = DesktopStringResources.Get("Shell.PlayerBar.Queue.Hint");

    public string SurfaceToken { get; } = DesktopDesignTokens.Surface.PlayerBar;

    public string TitleTypographyToken { get; } = DesktopDesignTokens.Typography.PlayerTitle;

    public string ArtistTypographyToken { get; } = DesktopDesignTokens.Typography.PlayerSubtitle;

    public string QueueIconToken { get; } = DesktopDesignTokens.Icon.PlayerQueue;

    public string PaddingToken { get; } = DesktopDesignTokens.Spacing.PlayerBarPadding;
}
