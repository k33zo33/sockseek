namespace Sockseek.Infrastructure.Persistence;

public sealed record SoulseekShareRootRecord(
    Guid Id,
    string Path,
    string VirtualName,
    bool Enabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
