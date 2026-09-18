namespace Sockseek.Application.Playback;

public sealed record PlaybackSourceResolution(
    PlaybackSourceKind Kind,
    Guid? CanonicalTrackId,
    Guid? PlaylistItemId,
    Guid? LocalMediaFileId,
    string? Path,
    string? Reason)
{
    public static PlaybackSourceResolution LocalFile(
        Guid canonicalTrackId,
        Guid? playlistItemId,
        Guid localMediaFileId,
        string path)
        => new(
            PlaybackSourceKind.LocalFile,
            canonicalTrackId,
            playlistItemId,
            localMediaFileId,
            path,
            null);

    public static PlaybackSourceResolution PendingResolution(Guid? playlistItemId, string reason)
        => new(PlaybackSourceKind.PendingResolution, null, playlistItemId, null, null, reason);

    public static PlaybackSourceResolution Unavailable(Guid? canonicalTrackId, Guid? playlistItemId, string reason)
        => new(PlaybackSourceKind.Unavailable, canonicalTrackId, playlistItemId, null, null, reason);
}
