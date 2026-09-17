namespace Sockseek.Infrastructure.LocalLibrary;

public sealed record LocalLibraryScanProgress(
    int DiscoveredFiles,
    int ScannedFiles,
    int ImportedFiles,
    int SkippedFiles,
    int FailedFiles,
    int MissingFiles,
    string? CurrentPath);
