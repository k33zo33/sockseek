using Sockseek.Domain.Accounts;

namespace Sockseek.Domain.Tracks;

public sealed class TrackIdentityService
{
    private readonly TrackIdentityOptions options;

    public TrackIdentityService(TrackIdentityOptions? options = null)
        => this.options = options ?? TrackIdentityOptions.Default;

    public TrackMatchResult Match(CanonicalTrack candidate, TrackIdentityQuery query)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(query);

        string? normalizedIsrc = NormalizeCode(query.Isrc);
        if (normalizedIsrc != null
            && candidate.Isrc != null
            && string.Equals(candidate.Isrc, normalizedIsrc, StringComparison.Ordinal))
        {
            return DurationCompatible(candidate.DurationMs, query.DurationMs)
                ? new TrackMatchResult(TrackMatchDisposition.AutoMatch, TrackMatchMethod.Isrc, 1.0d)
                : TrackMatchResult.NoMatch;
        }

        string? normalizedMbid = NormalizeCode(query.MusicBrainzRecordingId);
        if (normalizedMbid != null
            && candidate.MusicBrainzRecordingId != null
            && string.Equals(candidate.MusicBrainzRecordingId, normalizedMbid, StringComparison.Ordinal))
        {
            return new TrackMatchResult(TrackMatchDisposition.AutoMatch, TrackMatchMethod.MusicBrainzRecordingId, 0.99d);
        }

        if (query.SourceProvider is { } provider
            && !string.IsNullOrWhiteSpace(query.SourceExternalId)
            && candidate.Sources.Any(source => source.Provider == provider
                && string.Equals(source.ExternalId, query.SourceExternalId.Trim(), StringComparison.Ordinal)))
        {
            return new TrackMatchResult(TrackMatchDisposition.AutoMatch, TrackMatchMethod.PreviousSourceMapping, 1.0d);
        }

        var normalizedArtist = CanonicalTrack.NormalizeForMatch(query.Artist);
        var normalizedTitle = CanonicalTrack.NormalizeForMatch(query.Title);
        bool artistMatches = string.Equals(candidate.NormalizedArtist, normalizedArtist, StringComparison.Ordinal);
        bool titleMatches = string.Equals(candidate.NormalizedTitle, normalizedTitle, StringComparison.Ordinal);

        if (!artistMatches || !titleMatches)
            return TrackMatchResult.NoMatch;

        if (DurationCompatible(candidate.DurationMs, query.DurationMs))
        {
            return Evaluate(0.45d + 0.40d + 0.15d, TrackMatchMethod.NormalizedArtistTitleDuration);
        }

        return Evaluate(0.88d, TrackMatchMethod.NormalizedArtistTitle);
    }

    private TrackMatchResult Evaluate(double score, TrackMatchMethod method)
    {
        if (score >= options.AutoMatchThreshold)
            return new TrackMatchResult(TrackMatchDisposition.AutoMatch, method, score);

        if (score >= options.ReviewThreshold)
            return new TrackMatchResult(TrackMatchDisposition.ReviewRequired, method, score);

        return TrackMatchResult.NoMatch;
    }

    private bool DurationCompatible(int? leftDurationMs, int? rightDurationMs)
    {
        if (!leftDurationMs.HasValue || !rightDurationMs.HasValue)
            return false;

        return Math.Abs(leftDurationMs.Value - rightDurationMs.Value) <= options.DurationToleranceMs;
    }

    private static string? NormalizeCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
