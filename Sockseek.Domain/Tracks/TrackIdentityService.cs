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

        double versionConflictPenalty = CalculateVersionConflictPenalty(candidate.Title, query.Title);

        if (DurationCompatible(candidate.DurationMs, query.DurationMs))
        {
            return Evaluate((0.45d + 0.40d + 0.15d) - versionConflictPenalty, TrackMatchMethod.NormalizedArtistTitleDuration);
        }

        return Evaluate(0.88d - versionConflictPenalty, TrackMatchMethod.NormalizedArtistTitle);
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

    private static double CalculateVersionConflictPenalty(string candidateTitle, string queryTitle)
    {
        var candidateTags = ExtractVersionTags(candidateTitle);
        var queryTags = ExtractVersionTags(queryTitle);

        if (candidateTags.SetEquals(queryTags))
            return 0d;

        if (candidateTags.Count == 0 || queryTags.Count == 0)
            return 0.25d;

        return 0.20d;
    }

    private static HashSet<string> ExtractVersionTags(string title)
    {
        var normalized = CanonicalTrack.NormalizeForMatch(title);
        var tags = new HashSet<string>(StringComparer.Ordinal);

        foreach (string token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (token)
            {
                case "live":
                case "remix":
                case "mix":
                case "edit":
                case "version":
                case "acoustic":
                case "instrumental":
                case "remaster":
                case "remastered":
                    tags.Add(token);
                    break;
            }
        }

        return tags;
    }

    private static string? NormalizeCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
