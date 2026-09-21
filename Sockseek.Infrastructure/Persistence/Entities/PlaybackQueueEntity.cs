namespace Sockseek.Infrastructure.Persistence.Entities;

using Sockseek.Infrastructure.Persistence.Abstractions;

public sealed class PlaybackQueueEntity : IHasConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CurrentIndex { get; set; }
    public int RepeatMode { get; set; }
    public bool ShuffleEnabled { get; set; }
    public int ShuffleSeed { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<PlaybackQueueItemEntity> Items { get; } = [];
}
