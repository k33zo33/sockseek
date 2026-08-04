namespace Sockseek.Domain.Tracks;

public enum TrackMatchMethod
{
    None = 0,
    Isrc = 1,
    MusicBrainzRecordingId = 2,
    PreviousSourceMapping = 3,
    NormalizedArtistTitleDuration = 4,
    NormalizedArtistTitle = 5,
}
