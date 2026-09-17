namespace Sockseek.Infrastructure.LocalLibrary;

public sealed record LocalLibraryScanResult(
    int DiscoveredFiles,
    int ScannedFiles,
    int ImportedFiles,
    int SkippedFiles,
    int FailedFiles,
    int MissingFiles);
