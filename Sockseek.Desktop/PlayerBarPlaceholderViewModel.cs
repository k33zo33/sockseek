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

    public string PreviousIconToken { get; } = DesktopDesignTokens.Icon.PlayerPrevious;

    public bool CanPlayPause { get; } = false;

    public string PlayPauseIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.PlayPause.IconLabel";

    public string PlayPauseIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.PlayPause.IconLabel");

    public string PlayPauseHintResourceKey { get; } = "Shell.PlayerBar.PlayPause.Hint";

    public string PlayPauseHint { get; } = DesktopStringResources.Get("Shell.PlayerBar.PlayPause.Hint");

    public string PlayPauseIconToken { get; } = DesktopDesignTokens.Icon.PlayerPlayPause;

    public bool CanGoNext { get; } = false;

    public string NextIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.Next.IconLabel";

    public string NextIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.Next.IconLabel");

    public string NextIconToken { get; } = DesktopDesignTokens.Icon.PlayerNext;

    public string QueueSummaryResourceKey { get; } = "Shell.PlayerBar.QueueSummary";

    public string QueueSummary { get; } = DesktopStringResources.Get("Shell.PlayerBar.QueueSummary");

    public string QueueIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.Queue.IconLabel";

    public string QueueIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.Queue.IconLabel");

    public string QueueHintResourceKey { get; } = "Shell.PlayerBar.Queue.Hint";

    public string QueueHint { get; } = DesktopStringResources.Get("Shell.PlayerBar.Queue.Hint");

    public string VolumeIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.Volume.IconLabel";

    public string VolumeIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.Volume.IconLabel");

    public string VolumeHintResourceKey { get; } = "Shell.PlayerBar.Volume.Hint";

    public string VolumeHint { get; } = DesktopStringResources.Get("Shell.PlayerBar.Volume.Hint");

    public string ExpandedPlayerIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.ExpandedPlayer.IconLabel";

    public string ExpandedPlayerIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.ExpandedPlayer.IconLabel");

    public string ExpandedPlayerHintResourceKey { get; } = "Shell.PlayerBar.ExpandedPlayer.Hint";

    public string ExpandedPlayerHint { get; } = DesktopStringResources.Get("Shell.PlayerBar.ExpandedPlayer.Hint");

    public string SurfaceToken { get; } = DesktopDesignTokens.Surface.PlayerBar;

    public string TitleTypographyToken { get; } = DesktopDesignTokens.Typography.PlayerTitle;

    public string ArtistTypographyToken { get; } = DesktopDesignTokens.Typography.PlayerSubtitle;

    public string QueueIconToken { get; } = DesktopDesignTokens.Icon.PlayerQueue;

    public string VolumeIconToken { get; } = DesktopDesignTokens.Icon.PlayerVolume;

    public string ExpandedPlayerIconToken { get; } = DesktopDesignTokens.Icon.PlayerExpanded;

    public string PaddingToken { get; } = DesktopDesignTokens.Spacing.PlayerBarPadding;
}
