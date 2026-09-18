namespace Sockseek.Player;

public enum PlaybackState
{
    Stopped = 0,
    ResolvingSource = 1,
    Loading = 2,
    Playing = 3,
    Paused = 4,
    Failed = 5,
}
