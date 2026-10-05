using System.Text.RegularExpressions;

namespace Sockseek.Application.Security;

public static partial class SensitiveLogRedactor
{
    private const string Redacted = "[REDACTED]";

    public static string Redact(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        var withoutAuthorization = AuthorizationHeaderPattern().Replace(
            message,
            match => $"{match.Groups["prefix"].Value}{match.Groups["scheme"].Value} {Redacted}");

        return SensitiveKeyValuePattern().Replace(withoutAuthorization, match =>
        {
            var value = match.Groups["value"].Value;
            var quote = value.Length > 0 && (value[0] == '"' || value[0] == '\'')
                ? value[0].ToString()
                : string.Empty;
            return $"{match.Groups["prefix"].Value}{quote}{Redacted}{quote}";
        });
    }

    [GeneratedRegex(
        @"(?<prefix>\bAuthorization\s*[:=]\s*)(?<scheme>Bearer|Basic)\s+[^,\s;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeaderPattern();

    [GeneratedRegex(
        @"(?<prefix>[""']?(?:access_token|accesstoken|refresh_token|refreshtoken|code_verifier|codeverifier|client_secret|clientsecret|session_token|sessiontoken|soulseek_password|soulseekpassword|password|pass|code)[""']?\s*[:=]\s*)(?<value>""[^""]*""|'[^']*'|[^&\s,;]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveKeyValuePattern();
}
