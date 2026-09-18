using Microsoft.EntityFrameworkCore;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.LocalLibrary;

public sealed class LocalMediaFileRelinker(
    SockseekDbContext dbContext,
    ILocalAudioMetadataReader metadataReader)
{
    public async Task<LocalMediaFileRelinkResult?> RelinkAsync(
        Guid localMediaFileId,
        string newPath,
        CancellationToken cancellationToken = default)
    {
        string normalizedPath = NormalizePath(Path.GetFullPath(newPath));
        var fileInfo = new FileInfo(normalizedPath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException("Relink target does not exist.", normalizedPath);

        var entity = await dbContext.LocalMediaFiles.FindAsync([localMediaFileId], cancellationToken);
        if (entity == null)
            return null;

        bool pathTaken = await dbContext.LocalMediaFiles
            .AnyAsync(file => file.Id != localMediaFileId && file.Path == normalizedPath, cancellationToken);
        if (pathTaken)
            throw new InvalidOperationException("Another local media file already uses the relink target path.");

        var metadata = await metadataReader.ReadAsync(normalizedPath, cancellationToken);

        entity.Path = normalizedPath;
        entity.Size = fileInfo.Length;
        entity.LastWriteUtc = new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero);
        entity.DurationMs = metadata.DurationMs;
        entity.Codec = Normalize(metadata.Codec);
        entity.Bitrate = metadata.Bitrate;
        entity.SampleRate = metadata.SampleRate;
        entity.BitDepth = metadata.BitDepth;
        entity.Availability = (int)LocalMediaAvailability.Available;
        entity.ContentHash = null;
        entity.ContentHashAlgorithm = null;
        entity.ContentHashComputedAtUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new LocalMediaFileRelinkResult(entity.Id, entity.CanonicalTrackId, entity.Path);
    }

    private static string NormalizePath(string path)
        => path.Trim().Replace('\\', '/');

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
