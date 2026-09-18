namespace Sockseek.Infrastructure.LocalLibrary;

public enum LocalLibraryFileChangeKind
{
    Created,
    Changed,
    Deleted,
    Renamed,
}

public sealed record LocalLibraryFileChange(
    LocalLibraryFileChangeKind Kind,
    string Path,
    string? OldPath = null);
