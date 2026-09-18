using Microsoft.EntityFrameworkCore;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.LocalLibrary;

public sealed class LocalLibraryScanner(
    SockseekDbContext dbContext,
    CanonicalTrackStore trackStore,
    ILocalAudioMetadataReader metadataReader)
{
    public static readonly IReadOnlySet<string> DefaultSupportedExtensions = new HashSet<string>(
        [".aac", ".aiff", ".flac", ".m4a", ".mp3", ".ogg", ".opus", ".wav", ".wma"],
        StringComparer.OrdinalIgnoreCase);

    public async Task<LocalLibraryScanResult> ScanAsync(
        LocalLibraryScanRequest request,
        IProgress<LocalLibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var roots = NormalizeRoots(request.Roots);
        var extensions = request.SupportedExtensions ?? DefaultSupportedExtensions;
        var files = EnumerateSupportedFiles(roots, extensions).ToList();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int importedFiles = 0;
        int skippedFiles = 0;
        int failedFiles = 0;

        Report(progress, files.Count, 0, importedFiles, skippedFiles, failedFiles, 0, null);

        for (int index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string path = files[index];
            try
            {
                var fileInfo = new FileInfo(path);
                if (!fileInfo.Exists)
                {
                    skippedFiles++;
                    continue;
                }

                var metadata = await metadataReader.ReadAsync(path, cancellationToken);
                await trackStore.UpsertAsync(CreateTrackRecord(fileInfo, metadata), cancellationToken);
                seenPaths.Add(NormalizePath(fileInfo.FullName));
                importedFiles++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                failedFiles++;
            }
            finally
            {
                Report(progress, files.Count, index + 1, importedFiles, skippedFiles, failedFiles, 0, path);
            }
        }

        int missingFiles = await MarkMissingFilesAsync(roots, seenPaths, cancellationToken);
        Report(progress, files.Count, files.Count, importedFiles, skippedFiles, failedFiles, missingFiles, null);

        return new LocalLibraryScanResult(files.Count, files.Count, importedFiles, skippedFiles, failedFiles, missingFiles);
    }

    private static IReadOnlyList<string> NormalizeRoots(IReadOnlyList<string> roots)
    {
        if (roots.Count == 0)
            throw new ArgumentException("At least one library root is required.", nameof(roots));

        return roots
            .Select(root => string.IsNullOrWhiteSpace(root)
                ? throw new ArgumentException("Library root paths are required.", nameof(roots))
                : NormalizeDirectoryPath(Path.GetFullPath(root)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> EnumerateSupportedFiles(IReadOnlyList<string> roots, IReadOnlySet<string> supportedExtensions)
    {
        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (supportedExtensions.Contains(Path.GetExtension(path)))
                    yield return Path.GetFullPath(path);
            }
        }
    }

    private static CanonicalTrackRecord CreateTrackRecord(FileInfo fileInfo, LocalAudioMetadata metadata)
        => new(
            Fallback(metadata.Artist, "Unknown Artist"),
            Fallback(metadata.Title, Path.GetFileNameWithoutExtension(fileInfo.Name)),
            Normalize(metadata.AlbumTitle),
            metadata.DurationMs,
            metadata.Isrc,
            metadata.MusicBrainzRecordingId,
            [],
            [
                new LocalMediaFileRecord(
                    NormalizePath(fileInfo.FullName),
                    fileInfo.Length,
                    new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero),
                    metadata.DurationMs,
                    metadata.Codec,
                    metadata.Bitrate,
                    metadata.SampleRate,
                    metadata.BitDepth,
                    LocalMediaAvailability.Available)
            ]);

    private async Task<int> MarkMissingFilesAsync(
        IReadOnlyList<string> roots,
        HashSet<string> seenPaths,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.LocalMediaFiles
            .Where(file => file.Availability == (int)LocalMediaAvailability.Available)
            .ToListAsync(cancellationToken);

        int missingFiles = 0;
        foreach (var file in candidates)
        {
            if (!IsUnderAnyRoot(file.Path, roots) || seenPaths.Contains(file.Path))
                continue;

            file.Availability = (int)LocalMediaAvailability.Missing;
            missingFiles++;
        }

        if (missingFiles > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        return missingFiles;
    }

    private static bool IsUnderAnyRoot(string path, IReadOnlyList<string> roots)
    {
        string normalizedPath = NormalizePath(path);
        return roots.Any(root => normalizedPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeDirectoryPath(string path)
    {
        string normalized = NormalizePath(path).TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized + "/";
    }

    private static string NormalizePath(string path)
        => path.Trim().Replace('\\', '/');

    private static string Fallback(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Report(
        IProgress<LocalLibraryScanProgress>? progress,
        int discoveredFiles,
        int scannedFiles,
        int importedFiles,
        int skippedFiles,
        int failedFiles,
        int missingFiles,
        string? currentPath)
        => progress?.Report(new LocalLibraryScanProgress(
            discoveredFiles,
            scannedFiles,
            importedFiles,
            skippedFiles,
            failedFiles,
            missingFiles,
            currentPath));
}
