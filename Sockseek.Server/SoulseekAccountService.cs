using Sockseek.Api;
using Sockseek.Application.Security;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Server;

public sealed class SoulseekAccountService(
    SoulseekAccountStore accountStore,
    ISecretStore secretStore,
    EngineSupervisor supervisor)
{
    internal const int MaximumPasswordLength = 1024;
    private const string SecretProviderId = "soulseek";
    private const string PasswordKey = "password";

    public async Task<SoulseekAccountDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var account = await accountStore.GetAsync(cancellationToken);
        return CreateDto(account, supervisor.GetSoulseekAccountRuntimeSnapshot());
    }

    public async Task RestoreStoredCredentialsAsync(CancellationToken cancellationToken = default)
    {
        if (supervisor.GetSoulseekAccountRuntimeSnapshot().IsConfigured)
            return;

        var account = await accountStore.GetAsync(cancellationToken);
        if (account is null)
            return;

        var secret = await secretStore.ReadAsync(account.SecretReference, cancellationToken);
        if (secret is null
            || !secret.Secrets.TryGetValue(PasswordKey, out string? password)
            || string.IsNullOrEmpty(password))
        {
            return;
        }

        await supervisor.ChangeSoulseekCredentialsAsync(
            account.Username,
            password,
            persistChange: null,
            cancellationToken);
    }

    public async Task<SoulseekAccountDto> ConfigureAsync(
        ConfigureSoulseekAccountRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string username = SoulseekAccountStore.NormalizeUsername(request.Username);
        ValidatePassword(request.Password);

        string? newSecretReference = null;
        string? replacedSecretReference = null;
        bool metadataCommitted = false;
        try
        {
            await supervisor.ChangeSoulseekCredentialsAsync(
                username,
                request.Password,
                async ct =>
                {
                    var existing = await accountStore.GetAsync(ct);
                    replacedSecretReference = existing?.SecretReference;
                    newSecretReference = await secretStore.SaveAsync(
                        new SecretStoreSaveRequest(
                            SecretProviderId,
                            new Dictionary<string, string>(StringComparer.Ordinal)
                            {
                                [PasswordKey] = request.Password,
                            }),
                        ct);
                    try
                    {
                        await accountStore.SaveAsync(username, newSecretReference, ct);
                        metadataCommitted = true;
                    }
                    catch
                    {
                        await TryDeleteSecretAsync(newSecretReference);
                        newSecretReference = null;
                        throw;
                    }
                },
                cancellationToken);
        }
        finally
        {
            if (metadataCommitted
                && !string.IsNullOrWhiteSpace(replacedSecretReference)
                && !StringComparer.Ordinal.Equals(replacedSecretReference, newSecretReference))
            {
                await TryDeleteSecretAsync(replacedSecretReference);
            }
        }

        return await GetAsync(cancellationToken);
    }

    public async Task<SoulseekAccountDto> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        string? removedSecretReference = null;
        bool metadataRemoved = false;
        try
        {
            await supervisor.ChangeSoulseekCredentialsAsync(
                username: null,
                password: null,
                async ct =>
                {
                    var existing = await accountStore.GetAsync(ct);
                    removedSecretReference = existing?.SecretReference;
                    metadataRemoved = await accountStore.RemoveAsync(ct);
                },
                cancellationToken);
        }
        finally
        {
            if (metadataRemoved && !string.IsNullOrWhiteSpace(removedSecretReference))
                await TryDeleteSecretAsync(removedSecretReference);
        }

        return await GetAsync(cancellationToken);
    }

    private static SoulseekAccountDto CreateDto(
        SoulseekAccountRecord? account,
        SoulseekAccountRuntimeSnapshot runtime)
    {
        string credentialStatus = runtime.IsConfigured
            ? account is null ? "runtime_configured" : "configured"
            : account is null ? "not_configured" : "secret_unavailable";
        return new SoulseekAccountDto(
            account?.Username ?? runtime.Username,
            runtime.IsConfigured,
            runtime.IsConnected,
            runtime.ConnectionState,
            credentialStatus,
            account?.UpdatedAtUtc);
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Soulseek password is required.", nameof(password));
        if (password.Length > MaximumPasswordLength)
            throw new ArgumentException(
                $"Soulseek password cannot exceed {MaximumPasswordLength} characters.",
                nameof(password));
    }

    private async Task TryDeleteSecretAsync(string secretReference)
    {
        try
        {
            await secretStore.DeleteAsync(secretReference, CancellationToken.None);
        }
        catch (Exception)
        {
            Sockseek.Core.SockseekLog.Daemon.Warn("Failed to remove a superseded local secret payload.");
        }
    }
}
