namespace Sockseek.Domain.Playlists;

public enum PlaylistItemStatus
{
    Imported = 0,
    AvailableLocal = 1,
    ReviewRequired = 2,
    Unresolved = 3,
    Searching = 4,
    CandidateFound = 5,
    Downloading = 6,
    Failed = 7,
    Skipped = 8,
    RemovedFromSourcePlaylist = 9,
}
