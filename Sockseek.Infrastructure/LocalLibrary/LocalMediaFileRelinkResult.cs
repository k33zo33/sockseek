namespace Sockseek.Infrastructure.LocalLibrary;

public sealed record LocalMediaFileRelinkResult(
    Guid LocalMediaFileId,
    Guid? CanonicalTrackId,
    string Path);
