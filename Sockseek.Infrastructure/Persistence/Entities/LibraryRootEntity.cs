using Sockseek.Infrastructure.Persistence.Abstractions;

namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class LibraryRootEntity : IHasConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? LastScanStartedUtc { get; set; }
    public DateTimeOffset? LastScanCompletedUtc { get; set; }
}
