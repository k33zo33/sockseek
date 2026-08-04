namespace Sockseek.Domain.Accounts;

public enum ExternalAccountStatus
{
    PendingAuthorization = 0,
    Authorized = 1,
    AuthorizationExpired = 2,
    Disconnected = 3,
}
