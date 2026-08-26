using Sockseek.Application.Common;

namespace Sockseek.Infrastructure;

public sealed class GuidIdGenerator : IIdGenerator
{
    public Guid NewGuid() => Guid.NewGuid();
}
