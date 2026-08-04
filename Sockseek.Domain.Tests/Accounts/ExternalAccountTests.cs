using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Accounts;

namespace Sockseek.Domain.Tests.Accounts;

[TestClass]
public class ExternalAccountTests
{
    [TestMethod]
    public void Reauthorize_UpdatesSecretReference_AndRestoresAuthorizedStatus()
    {
        var account = new ExternalAccount(
            ExternalProvider.Spotify,
            "user-1",
            "Alice",
            "secret://first",
            new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero));

        account.ExpireAuthorization();
        account.Reauthorize(
            "Alice Updated",
            "secret://second",
            new DateTimeOffset(2026, 8, 4, 19, 0, 0, TimeSpan.Zero));

        Assert.AreEqual(ExternalAccountStatus.Authorized, account.Status);
        Assert.AreEqual("Alice Updated", account.DisplayName);
        Assert.AreEqual("secret://second", account.SecretReference);
        Assert.AreEqual(new DateTimeOffset(2026, 8, 4, 19, 0, 0, TimeSpan.Zero), account.LastAuthorizedAtUtc);
    }

    [TestMethod]
    public void Disconnect_ClearsSecretReference_WithoutKeepingTokenLikeProperties()
    {
        var account = new ExternalAccount(
            ExternalProvider.YouTube,
            "user-2",
            "Bob",
            "secret://token-ref",
            new DateTimeOffset(2026, 8, 4, 18, 0, 0, TimeSpan.Zero));

        account.Disconnect();

        Assert.AreEqual(ExternalAccountStatus.Disconnected, account.Status);
        Assert.AreEqual(string.Empty, account.SecretReference);

        var forbiddenProperty = typeof(ExternalAccount)
            .GetProperties()
            .Select(property => property.Name)
            .FirstOrDefault(name => name.Contains("token", StringComparison.OrdinalIgnoreCase)
                || name.Contains("refresh", StringComparison.OrdinalIgnoreCase)
                || name.Contains("oauth", StringComparison.OrdinalIgnoreCase));

        Assert.IsNull(forbiddenProperty);
    }
}
