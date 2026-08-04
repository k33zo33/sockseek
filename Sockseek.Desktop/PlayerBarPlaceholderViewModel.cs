namespace Sockseek.Desktop;

public sealed class PlayerBarPlaceholderViewModel
{
    public string Title { get; } = "Nothing playing";

    public string Artist { get; } = "Choose a local track or completed download";

    public bool CanGoPrevious { get; } = false;

    public bool CanPlayPause { get; } = false;

    public bool CanGoNext { get; } = false;

    public string QueueSummary { get; } = "Queue unavailable until playback coordinator is connected";
}
