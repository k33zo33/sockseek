namespace Sockseek.Infrastructure.Persistence.Entities;

using Sockseek.Infrastructure.Persistence.Abstractions;

public sealed class AppSettingEntity : IHasConcurrencyToken
{
    public string Key { get; set; } = string.Empty;
    public Guid ConcurrencyToken { get; set; }
    public string JsonValue { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
