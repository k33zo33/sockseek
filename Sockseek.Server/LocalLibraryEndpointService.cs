using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sockseek.Api;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure;
using Sockseek.Infrastructure.LocalLibrary;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Server;

public sealed class LocalLibraryEndpointService(IOptions<ServerOptions> options)
{
    private readonly SemaphoreSlim migrationLock = new(1, 1);
    private bool migrated;

    public async Task<IReadOnlyList<LibraryRootDto>> ListRootsAsync(CancellationToken cancellationToken)
    {
        await EnsureMigratedAsync(cancellationToken);
        await using var context = CreateContext();
        return (await new LibraryRootStore(context, new SystemClock()).ListAsync(cancellationToken))
            .Select(root => root.ToDto())
            .ToList();
    }

    public async Task<LibraryRootDto> SaveRootAsync(SaveLibraryRootRequestDto request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureMigratedAsync(cancellationToken);
        await using var context = CreateContext();
        var store = new LibraryRootStore(context, new SystemClock());
        var id = await store.AddOrUpdateAsync(request.Path, request.DisplayName, request.Enabled, cancellationToken);
        return (await store.ListAsync(cancellationToken)).Single(root => root.Id == id).ToDto();
    }

    public async Task<bool> DeleteRootAsync(Guid rootId, CancellationToken cancellationToken)
    {
        await EnsureMigratedAsync(cancellationToken);
        await using var context = CreateContext();
        return await new LibraryRootStore(context, new SystemClock()).RemoveAsync(rootId, cancellationToken);
    }

    public async Task<LocalLibraryConfiguredScanResultDto> ScanAsync(CancellationToken cancellationToken)
    {
        await EnsureMigratedAsync(cancellationToken);
        await using var rootContext = CreateContext();
        await using var scannerContext = CreateContext();
        var rootStore = new LibraryRootStore(rootContext, new SystemClock());
        var scanner = new LocalLibraryScanner(scannerContext, new CanonicalTrackStore(scannerContext), new TagLibAudioMetadataReader());
        var result = await new LocalLibraryScanCoordinator(rootStore, scanner).ScanEnabledRootsAsync(cancellationToken: cancellationToken);
        return ToDto(result);
    }

    public async Task<LocalLibrarySearchResponseDto> SearchTracksAsync(
        string? searchText,
        int offset,
        int limit,
        bool includeMissing,
        CancellationToken cancellationToken)
    {
        await EnsureMigratedAsync(cancellationToken);
        await using var context = CreateContext();
        var result = await new LocalLibraryQueryStore(context)
            .SearchAsync(new LocalLibrarySearchRequest(searchText, offset, limit, includeMissing), cancellationToken);
        return ToDto(result);
    }

    public async Task<IReadOnlyList<LocalLibraryDuplicateGroupDto>> GetDuplicateGroupsAsync(
        int limit,
        bool includeMissing,
        CancellationToken cancellationToken)
    {
        await EnsureMigratedAsync(cancellationToken);
        await using var context = CreateContext();
        return (await new LocalLibraryQueryStore(context).GetDuplicateGroupsAsync(limit, includeMissing, cancellationToken))
            .Select(ToDto)
            .ToList();
    }

    public async Task<LocalMediaFileRelinkResultDto?> RelinkAsync(
        Guid localMediaFileId,
        RelinkLocalMediaFileRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureMigratedAsync(cancellationToken);
        await using var context = CreateContext();
        var result = await new LocalMediaFileRelinker(context, new TagLibAudioMetadataReader())
            .RelinkAsync(localMediaFileId, request.Path, cancellationToken);
        return result == null ? null : new LocalMediaFileRelinkResultDto(result.LocalMediaFileId, result.CanonicalTrackId, result.Path);
    }

    private async Task EnsureMigratedAsync(CancellationToken cancellationToken)
    {
        if (migrated)
            return;

        await migrationLock.WaitAsync(cancellationToken);
        try
        {
            if (migrated)
                return;

            string databasePath = ResolveDatabasePath();
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            string backupDirectory = options.Value.DatabaseBackupDir
                ?? Path.Combine(Path.GetDirectoryName(databasePath)!, "backups");
            var runner = new SqliteMigrationRunner(CreateContext);
            await runner.MigrateAsync(databasePath, backupDirectory, cancellationToken);
            migrated = true;
        }
        finally
        {
            migrationLock.Release();
        }
    }

    private SockseekDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<SockseekDbContext>();
        builder.UseSqlite($"Data Source={ResolveDatabasePath()}");
        return new SockseekDbContext(builder.Options);
    }

    private string ResolveDatabasePath()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.DatabasePath))
            return Path.GetFullPath(options.Value.DatabasePath);

        string baseDirectory = !string.IsNullOrWhiteSpace(options.Value.ConfigDir)
            ? options.Value.ConfigDir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sockseek");
        return Path.GetFullPath(Path.Combine(baseDirectory, "sockseek.db"));
    }

    private static LocalLibraryConfiguredScanResultDto ToDto(LocalLibraryConfiguredScanResult result)
        => new(result.RootIds, ToDto(result.ScanResult));

    private static LocalLibraryScanResultDto ToDto(LocalLibraryScanResult result)
        => new(
            result.DiscoveredFiles,
            result.ScannedFiles,
            result.ImportedFiles,
            result.SkippedFiles,
            result.FailedFiles,
            result.MissingFiles);

    private static LocalLibrarySearchResponseDto ToDto(LocalLibrarySearchResult result)
        => new(result.TotalCount, result.Items.Select(ToDto).ToList());

    private static LocalLibraryTrackDto ToDto(LocalLibraryTrackRecord record)
        => new(
            record.TrackId,
            record.Artist,
            record.Title,
            record.DurationMs,
            record.Isrc,
            record.MusicBrainzRecordingId,
            record.AvailableFileCount,
            record.MissingFileCount,
            record.BestAvailableFileId,
            record.BestAvailablePath,
            record.Codec,
            record.Bitrate,
            record.SampleRate,
            record.BitDepth);

    private static LocalLibraryDuplicateGroupDto ToDto(LocalLibraryDuplicateGroupRecord record)
        => new(
            record.TrackId,
            record.Artist,
            record.Title,
            record.DurationMs,
            record.FileCount,
            record.Files.Select(ToDto).ToList());

    private static LocalLibraryDuplicateFileDto ToDto(LocalLibraryDuplicateFileRecord record)
        => new(
            record.LocalMediaFileId,
            record.Path,
            record.Size,
            record.DurationMs,
            record.Codec,
            record.Bitrate,
            record.SampleRate,
            record.BitDepth,
            ToDto(record.Availability));

    private static LocalMediaAvailabilityDto ToDto(LocalMediaAvailability availability)
        => availability switch
        {
            LocalMediaAvailability.Available => LocalMediaAvailabilityDto.Available,
            LocalMediaAvailability.Missing => LocalMediaAvailabilityDto.Missing,
            _ => throw new ArgumentOutOfRangeException(nameof(availability), availability, null),
        };
}

file static class LibraryRootRecordExtensions
{
    public static LibraryRootDto ToDto(this LibraryRootRecord record)
        => new(
            record.Id,
            record.Path,
            record.DisplayName,
            record.Enabled,
            record.CreatedAtUtc,
            record.UpdatedAtUtc,
            record.LastScanStartedUtc,
            record.LastScanCompletedUtc);
}
