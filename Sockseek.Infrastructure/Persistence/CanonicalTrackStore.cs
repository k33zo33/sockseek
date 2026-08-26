using Microsoft.EntityFrameworkCore;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class CanonicalTrackStore(SockseekDbContext dbContext)
{
    public async Task<Guid> UpsertAsync(CanonicalTrackRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        string normalizedArtist = NormalizeForMatch(record.Artist);
        string normalizedTitle = NormalizeForMatch(record.Title);
        string? normalizedIsrc = NormalizeCode(record.Isrc);
        string? normalizedMbid = NormalizeCode(record.MusicBrainzRecordingId);

        var query = dbContext.CanonicalTracks
            .Include(entity => entity.Sources)
            .Include(entity => entity.LocalMediaFiles)
            .AsQueryable();

        CanonicalTrackEntity? entity = null;
        if (normalizedMbid != null)
        {
            entity = await query.SingleOrDefaultAsync(x => x.MusicBrainzRecordingId == normalizedMbid, cancellationToken);
        }

        entity ??= normalizedIsrc != null
            ? await query.SingleOrDefaultAsync(x => x.Isrc == normalizedIsrc, cancellationToken)
            : null;

        entity ??= await query.SingleOrDefaultAsync(
            x => x.NormalizedArtist == normalizedArtist
                && x.NormalizedTitle == normalizedTitle
                && x.DurationMs == record.DurationMs,
            cancellationToken);

        if (entity == null)
        {
            entity = new CanonicalTrackEntity
            {
                Id = Guid.NewGuid(),
                Artist = record.Artist.Trim(),
                Title = record.Title.Trim(),
                DurationMs = record.DurationMs,
                Isrc = normalizedIsrc,
                MusicBrainzRecordingId = normalizedMbid,
                NormalizedArtist = normalizedArtist,
                NormalizedTitle = normalizedTitle,
            };
            dbContext.CanonicalTracks.Add(entity);
        }
        else
        {
            entity.Artist = record.Artist.Trim();
            entity.Title = record.Title.Trim();
            entity.DurationMs = record.DurationMs;
            entity.Isrc ??= normalizedIsrc;
            entity.MusicBrainzRecordingId ??= normalizedMbid;
            entity.NormalizedArtist = normalizedArtist;
            entity.NormalizedTitle = normalizedTitle;
        }

        foreach (var source in record.Sources)
        {
            var existingSource = entity.Sources.SingleOrDefault(
                x => x.Provider == (int)source.Provider && x.ExternalId == source.ExternalId);

            if (existingSource == null)
            {
                entity.Sources.Add(new TrackSourceEntity
                {
                    Id = Guid.NewGuid(),
                    Provider = (int)source.Provider,
                    ExternalId = source.ExternalId,
                    ExternalUrl = Normalize(source.ExternalUrl),
                    RawMetadataJson = Normalize(source.RawMetadataJson),
                });
            }
            else
            {
                existingSource.ExternalUrl = Normalize(source.ExternalUrl);
                existingSource.RawMetadataJson = Normalize(source.RawMetadataJson);
            }
        }

        foreach (var file in record.LocalMediaFiles)
        {
            string normalizedPath = NormalizePath(file.Path);
            var existingFile = entity.LocalMediaFiles.SingleOrDefault(x => x.Path == normalizedPath)
                ?? await dbContext.LocalMediaFiles.SingleOrDefaultAsync(x => x.Path == normalizedPath, cancellationToken);

            if (existingFile == null)
            {
                entity.LocalMediaFiles.Add(new LocalMediaFileEntity
                {
                    Id = Guid.NewGuid(),
                    Path = normalizedPath,
                    Size = file.Size,
                    LastWriteUtc = file.LastWriteUtc,
                    DurationMs = file.DurationMs,
                    Codec = Normalize(file.Codec),
                    Bitrate = file.Bitrate,
                    SampleRate = file.SampleRate,
                    BitDepth = file.BitDepth,
                    Availability = (int)file.Availability,
                });
            }
            else
            {
                existingFile.CanonicalTrackId = entity.Id;
                existingFile.Size = file.Size;
                existingFile.LastWriteUtc = file.LastWriteUtc;
                existingFile.DurationMs = file.DurationMs;
                existingFile.Codec = Normalize(file.Codec);
                existingFile.Bitrate = file.Bitrate;
                existingFile.SampleRate = file.SampleRate;
                existingFile.BitDepth = file.BitDepth;
                existingFile.Availability = (int)file.Availability;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizePath(string path)
        => string.IsNullOrWhiteSpace(path)
            ? throw new ArgumentException("path is required.", nameof(path))
            : path.Trim().Replace('\\', '/');

    private static string NormalizeForMatch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("value is required.", nameof(value));

        var chars = value.Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();

        return string.Join(' ', new string(chars)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string? NormalizeCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
