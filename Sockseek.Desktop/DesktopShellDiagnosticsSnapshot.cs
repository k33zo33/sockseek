using System.Text.RegularExpressions;

namespace Sockseek.Desktop;

public sealed partial record DesktopShellDiagnosticsSnapshot(
    string WindowTitle,
    string CurrentPageTitle,
    string Theme,
    string BackendState,
    string BackendBannerTitle,
    bool HasHandshake,
    string? BackendBaseUrl)
{
    public string ToDisplayText()
        => Redact(string.Join(
            Environment.NewLine,
            [
                $"Window: {WindowTitle}",
                $"Page: {CurrentPageTitle}",
                $"Theme: {Theme}",
                $"Backend state: {BackendState}",
                $"Backend banner: {BackendBannerTitle}",
                $"Handshake present: {HasHandshake}",
                $"Backend URL: {BackendBaseUrl ?? "unavailable"}"
            ]));

    private static string Redact(string text)
    {
        var withoutAuthorization = AuthorizationHeaderPattern().Replace(
            text,
            match => $"{match.Groups["prefix"].Value}{match.Groups["scheme"].Value} [REDACTED]");

        return SensitiveKeyValuePattern().Replace(withoutAuthorization, match =>
        {
            var value = match.Groups["value"].Value;
            var quote = value.Length > 0 && (value[0] == '"' || value[0] == '\'')
                ? value[0].ToString()
                : string.Empty;
            return $"{match.Groups["prefix"].Value}{quote}[REDACTED]{quote}";
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
