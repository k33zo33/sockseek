namespace Sockseek.Infrastructure.Persistence;

public sealed record PlaylistLocalMatchResult(
    int MatchedItems,
    int ReviewItems,
    int UnresolvedItems);
