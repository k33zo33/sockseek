using Sockseek.Application.Security;

namespace Sockseek.Infrastructure.Security;

public static class PlatformSecretStoreFactory
{
    public static ISecretStore Create(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return OperatingSystem.IsWindows() && WindowsDpapiSecretStore.IsAvailable()
            ? new WindowsDpapiSecretStore(directory)
            : new LocalEncryptedFileSecretStore(directory);
    }
}
