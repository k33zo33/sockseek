namespace Sockseek.Domain.Tracks;

public sealed class TrackIdentityOptions
{
    public static TrackIdentityOptions Default { get; } = new();

    public double AutoMatchThreshold { get; init; } = 0.92;
    public double ReviewThreshold { get; init; } = 0.75;
    public int DurationToleranceMs { get; init; } = 10_000;
}
