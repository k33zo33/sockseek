using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Security;

namespace Tests.Application.Security;

[TestClass]
public sealed class SensitiveLogRedactorTests
{
    [TestMethod]
    public void Redact_MasksAuthorizationHeader()
    {
        var redacted = SensitiveLogRedactor.Redact("Authorization: Bearer abc.def.ghi");

        Assert.AreEqual("Authorization: Bearer [REDACTED]", redacted);
        Assert.IsFalse(redacted.Contains("abc.def.ghi", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_MasksOAuthQueryParameters()
    {
        var redacted = SensitiveLogRedactor.Redact(
            "callback?code=oauth-code&state=public-state&access_token=raw-access-token&refresh_token=raw-refresh-token");

        StringAssert.Contains(redacted, "code=[REDACTED]");
        StringAssert.Contains(redacted, "state=public-state");
        StringAssert.Contains(redacted, "access_token=[REDACTED]");
        StringAssert.Contains(redacted, "refresh_token=[REDACTED]");
        Assert.IsFalse(redacted.Contains("oauth-code", StringComparison.Ordinal));
        Assert.IsFalse(redacted.Contains("raw-access-token", StringComparison.Ordinal));
        Assert.IsFalse(redacted.Contains("raw-refresh-token", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_MasksJsonLikeSecretFields()
    {
        var redacted = SensitiveLogRedactor.Redact(
            """{"client_secret":"secret-value","code_verifier":"verifier-value","SessionToken":"session-token","name":"public"}""");

        StringAssert.Contains(redacted, """"client_secret":"[REDACTED]"""");
        StringAssert.Contains(redacted, """"code_verifier":"[REDACTED]"""");
        StringAssert.Contains(redacted, """"SessionToken":"[REDACTED]"""");
        StringAssert.Contains(redacted, """"name":"public"""");
        Assert.IsFalse(redacted.Contains("secret-value", StringComparison.Ordinal));
        Assert.IsFalse(redacted.Contains("verifier-value", StringComparison.Ordinal));
        Assert.IsFalse(redacted.Contains("session-token", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Redact_MasksPasswordAndSessionQueryValues()
    {
        var redacted = SensitiveLogRedactor.Redact(
            "connect?password=soulseek-password&session_token=local-session&pass=short&state=public-state");

        StringAssert.Contains(redacted, "password=[REDACTED]");
        StringAssert.Contains(redacted, "session_token=[REDACTED]");
        StringAssert.Contains(redacted, "pass=[REDACTED]");
        StringAssert.Contains(redacted, "state=public-state");
        Assert.IsFalse(redacted.Contains("soulseek-password", StringComparison.Ordinal));
        Assert.IsFalse(redacted.Contains("local-session", StringComparison.Ordinal));
        Assert.IsFalse(redacted.Contains("short", StringComparison.Ordinal));
    }
}
