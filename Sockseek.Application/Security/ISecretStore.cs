namespace Sockseek.Application.Security;

public interface ISecretStore
{
    Task<string> SaveAsync(SecretStoreSaveRequest request, CancellationToken cancellationToken = default);

    Task<SecretStoreEntry?> ReadAsync(string secretReference, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string secretReference, CancellationToken cancellationToken = default);
}

public sealed record SecretStoreSaveRequest(
    string ProviderId,
    IReadOnlyDictionary<string, string> Secrets,
    DateTimeOffset? ExpiresAtUtc = null);

public sealed record SecretStoreEntry(
    string SecretReference,
    IReadOnlyDictionary<string, string> Secrets,
    DateTimeOffset? ExpiresAtUtc);
