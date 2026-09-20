namespace Sockseek.Player;

public sealed record PlaybackQueueSnapshot(
    IReadOnlyList<PlaybackQueueItem> Items,
    int CurrentIndex,
    PlaybackRepeatMode RepeatMode,
    bool ShuffleEnabled,
    int ShuffleSeed,
    IReadOnlyList<int> PlaybackOrder)
{
    public PlaybackQueueItem? CurrentItem
        => CurrentIndex >= 0 && CurrentIndex < Items.Count
            ? Items[CurrentIndex]
            : null;
}
