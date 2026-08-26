namespace Sockseek.Application.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
