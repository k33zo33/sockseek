namespace Sockseek.Infrastructure.Persistence;

public sealed record PlaybackQueueRecord(
    Guid Id,
    string Name,
    int CurrentIndex,
    PlaybackQueueRepeatMode RepeatMode,
    bool ShuffleEnabled,
    int ShuffleSeed,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<PlaybackQueueItemRecord> Items);
