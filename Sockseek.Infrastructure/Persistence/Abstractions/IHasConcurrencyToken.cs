namespace Sockseek.Infrastructure.Persistence.Abstractions;

public interface IHasConcurrencyToken
{
    Guid ConcurrencyToken { get; set; }
}
