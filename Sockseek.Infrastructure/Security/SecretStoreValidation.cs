using Sockseek.Application.Security;

namespace Sockseek.Infrastructure.Security;

internal static class SecretStoreValidation
{
    public static void Validate(SecretStoreSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProviderId);

        if (request.Secrets.Count == 0)
            throw new ArgumentException("At least one secret value is required.", nameof(request));

        foreach (var (key, value) in request.Secrets)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
        }
    }
}
