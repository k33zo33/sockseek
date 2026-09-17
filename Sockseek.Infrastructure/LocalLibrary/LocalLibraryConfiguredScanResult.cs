namespace Sockseek.Infrastructure.LocalLibrary;

public sealed record LocalLibraryConfiguredScanResult(
    IReadOnlyList<Guid> RootIds,
    LocalLibraryScanResult ScanResult);
