using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.LocalLibrary;

public sealed class LocalLibraryScanCoordinator(
    LibraryRootStore libraryRootStore,
    LocalLibraryScanner scanner)
{
    public async Task<LocalLibraryConfiguredScanResult> ScanEnabledRootsAsync(
        IProgress<LocalLibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var roots = (await libraryRootStore.ListAsync(cancellationToken))
            .Where(root => root.Enabled)
            .ToList();

        if (roots.Count == 0)
            return new LocalLibraryConfiguredScanResult([], new LocalLibraryScanResult(0, 0, 0, 0, 0, 0));

        foreach (var root in roots)
            await libraryRootStore.MarkScanStartedAsync(root.Id, cancellationToken);

        var scanResult = await scanner.ScanAsync(
            new LocalLibraryScanRequest(roots.Select(root => root.Path).ToList()),
            progress,
            cancellationToken);

        foreach (var root in roots)
            await libraryRootStore.MarkScanCompletedAsync(root.Id, cancellationToken);

        return new LocalLibraryConfiguredScanResult(roots.Select(root => root.Id).ToList(), scanResult);
    }
}
