namespace Sockseek.Infrastructure.Persistence;

public sealed record PlaybackQueueSaveRecord(
    Guid Id,
    string Name,
    int CurrentIndex,
    PlaybackQueueRepeatMode RepeatMode,
    bool ShuffleEnabled,
    int ShuffleSeed,
    IReadOnlyList<PlaybackQueueItemRecord> Items);
