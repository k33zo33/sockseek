using Microsoft.EntityFrameworkCore;
using Sockseek.Domain.Accounts;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class CanonicalTrackMetadataEnrichmentQueue(
    CanonicalTrackMetadataEnrichmentStore store)
{
    private readonly Queue<CanonicalTrackMetadataEnrichmentRecord> pending = [];
    private readonly object gate = new();

    public int PendingCount
    {
        get
        {
            lock (gate)
                return pending.Count;
        }
    }

    public void Enqueue(CanonicalTrackMetadataEnrichmentRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (gate)
            pending.Enqueue(record);
    }

    public async Task<CanonicalTrackMetadataEnrichmentQueueResult> DrainAsync(
        int? maxItems = null,
        CancellationToken cancellationToken = default)
    {
        int applied = 0;
        while (!maxItems.HasValue || applied < maxItems.Value)
        {
            CanonicalTrackMetadataEnrichmentRecord? record;
            lock (gate)
                record = pending.Count > 0 ? pending.Dequeue() : null;

            if (record == null)
                break;

            await store.ApplyAsync(record, cancellationToken).ConfigureAwait(false);
            applied++;
        }

        return new CanonicalTrackMetadataEnrichmentQueueResult(applied, PendingCount);
    }
}

public sealed class CanonicalTrackMetadataEnrichmentStore(SockseekDbContext dbContext)
{
    public async Task ApplyAsync(
        CanonicalTrackMetadataEnrichmentRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        DetachUnchangedTrackedCanonicalTrack(record.CanonicalTrackId);

        var track = await dbContext.CanonicalTracks
            .Include(entity => entity.Sources)
            .SingleOrDefaultAsync(entity => entity.Id == record.CanonicalTrackId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Canonical track '{record.CanonicalTrackId}' was not found.");

        string? normalizedIsrc = NormalizeCode(record.Isrc);
        string? normalizedMbid = NormalizeCode(record.MusicBrainzRecordingId);
        string externalId = NormalizeRequired(record.ExternalId, nameof(record.ExternalId));
        Guid databaseConcurrencyToken = await dbContext.CanonicalTracks
            .AsNoTracking()
            .Where(entity => entity.Id == record.CanonicalTrackId)
            .Select(entity => entity.ConcurrencyToken)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        track.ConcurrencyToken = databaseConcurrencyToken;
        dbContext.Entry(track)
            .Property(nameof(CanonicalTrackEntity.ConcurrencyToken))
            .OriginalValue = databaseConcurrencyToken;

        if (track.Isrc == null && normalizedIsrc != null)
            track.Isrc = normalizedIsrc;
        if (track.MusicBrainzRecordingId == null && normalizedMbid != null)
            track.MusicBrainzRecordingId = normalizedMbid;

        var existingSource = await dbContext.TrackSources.SingleOrDefaultAsync(
            source => source.Provider == (int)record.Provider && source.ExternalId == externalId,
            cancellationToken).ConfigureAwait(false);

        if (existingSource != null && existingSource.CanonicalTrackId != track.Id)
        {
            throw new InvalidOperationException(
                $"Metadata source '{record.Provider}:{externalId}' is already mapped to another canonical track.");
        }

        if (existingSource == null)
        {
            dbContext.TrackSources.Add(new TrackSourceEntity
            {
                Id = Guid.NewGuid(),
                CanonicalTrackId = track.Id,
                Provider = (int)record.Provider,
                ExternalId = externalId,
                ExternalUrl = Normalize(record.ExternalUrl),
                RawMetadataJson = Normalize(record.RawMetadataJson),
            });
        }
        else
        {
            existingSource.ExternalUrl = Normalize(record.ExternalUrl);
            existingSource.RawMetadataJson = Normalize(record.RawMetadataJson);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeRequired(string value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} is required.", paramName)
            : value.Trim();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private void DetachUnchangedTrackedCanonicalTrack(Guid canonicalTrackId)
    {
        var trackedEntries = dbContext.ChangeTracker
            .Entries<CanonicalTrackEntity>()
            .Where(entry => entry.Entity.Id == canonicalTrackId)
            .ToArray();

        foreach (var entry in trackedEntries)
        {
            if (entry.State != EntityState.Unchanged)
            {
                throw new InvalidOperationException(
                    $"Canonical track '{canonicalTrackId}' has pending changes and cannot be enriched safely.");
            }

            entry.State = EntityState.Detached;
        }
    }
}

public sealed record CanonicalTrackMetadataEnrichmentRecord(
    Guid CanonicalTrackId,
    ExternalProvider Provider,
    string ExternalId,
    string? ExternalUrl,
    string? Isrc,
    string? MusicBrainzRecordingId,
    string? RawMetadataJson);

public sealed record CanonicalTrackMetadataEnrichmentQueueResult(
    int AppliedItems,
    int RemainingItems);
