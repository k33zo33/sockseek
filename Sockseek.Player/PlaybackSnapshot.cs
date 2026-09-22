namespace Sockseek.Player;

public sealed record PlaybackSnapshot(
    PlaybackState State,
    Guid? CanonicalTrackId,
    Guid? PlaylistItemId,
    Guid? LocalMediaFileId,
    string? Path,
    string? ErrorMessage,
    TimeSpan Position,
    double Volume,
    bool IsMuted,
    PlaybackBufferSnapshot? Buffer = null)
{
    public static PlaybackSnapshot Stopped { get; } = new(
        PlaybackState.Stopped,
        null,
        null,
        null,
        null,
        null,
        TimeSpan.Zero,
        1.0,
        false,
        null);
}
