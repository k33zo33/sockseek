namespace Sockseek.Infrastructure.Persistence;

public sealed record LibraryRootRecord(
    Guid Id,
    string Path,
    string? DisplayName,
    bool Enabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastScanStartedUtc,
    DateTimeOffset? LastScanCompletedUtc);
