namespace Sockseek.Infrastructure.Persistence.Entities;

public sealed class AppSettingEntity
{
    public string Key { get; set; } = string.Empty;
    public string JsonValue { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
