namespace Sockseek.Infrastructure.Tests.TestSupport;

internal sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value)
        => handler(value);
}
