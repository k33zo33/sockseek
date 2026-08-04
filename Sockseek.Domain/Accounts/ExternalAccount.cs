using Sockseek.Domain.Common;

namespace Sockseek.Domain.Accounts;

public sealed class ExternalAccount
{
    public ExternalAccount(
        ExternalProvider provider,
        string externalUserId,
        string displayName,
        string secretReference,
        DateTimeOffset authorizedAtUtc)
    {
        Id = EntityId.New();
        Provider = provider;
        ExternalUserId = Require(externalUserId, nameof(externalUserId));
        DisplayName = Require(displayName, nameof(displayName));
        SecretReference = Require(secretReference, nameof(secretReference));
        LastAuthorizedAtUtc = authorizedAtUtc;
        Status = ExternalAccountStatus.Authorized;
    }

    public EntityId Id { get; }
    public ExternalProvider Provider { get; }
    public string ExternalUserId { get; }
    public string DisplayName { get; private set; }
    public string SecretReference { get; private set; }
    public ExternalAccountStatus Status { get; private set; }
    public DateTimeOffset? LastAuthorizedAtUtc { get; private set; }

    public void Reauthorize(string displayName, string secretReference, DateTimeOffset authorizedAtUtc)
    {
        DisplayName = Require(displayName, nameof(displayName));
        SecretReference = Require(secretReference, nameof(secretReference));
        LastAuthorizedAtUtc = authorizedAtUtc;
        Status = ExternalAccountStatus.Authorized;
    }

    public void ExpireAuthorization()
    {
        if (Status == ExternalAccountStatus.Disconnected)
            return;

        Status = ExternalAccountStatus.AuthorizationExpired;
    }

    public void Disconnect()
    {
        Status = ExternalAccountStatus.Disconnected;
        SecretReference = string.Empty;
    }

    private static string Require(string value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} is required.", paramName)
            : value.Trim();
}
