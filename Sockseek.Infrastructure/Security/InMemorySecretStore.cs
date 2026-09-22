using System.Collections.Concurrent;
using Sockseek.Application.Security;

namespace Sockseek.Infrastructure.Security;

public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, SecretStoreEntry> entries = new(StringComparer.Ordinal);

    public Task<string> SaveAsync(SecretStoreSaveRequest request, CancellationToken cancellationToken = default)
    {
        SecretStoreValidation.Validate(request);

        var reference = "secret://memory/" + Guid.NewGuid().ToString("N");
        entries[reference] = new SecretStoreEntry(
            reference,
            new Dictionary<string, string>(request.Secrets, StringComparer.Ordinal),
            request.ExpiresAtUtc);
        return Task.FromResult(reference);
    }

    public Task<SecretStoreEntry?> ReadAsync(string secretReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretReference);
        entries.TryGetValue(secretReference, out var entry);
        return Task.FromResult(entry);
    }

    public Task<bool> DeleteAsync(string secretReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretReference);
        return Task.FromResult(entries.TryRemove(secretReference, out _));
    }
}
