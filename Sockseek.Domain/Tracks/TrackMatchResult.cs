namespace Sockseek.Domain.Tracks;

public sealed record TrackMatchResult(
    TrackMatchDisposition Disposition,
    TrackMatchMethod Method,
    double Score)
{
    public static TrackMatchResult NoMatch { get; } = new(TrackMatchDisposition.NoMatch, TrackMatchMethod.None, 0d);
}
