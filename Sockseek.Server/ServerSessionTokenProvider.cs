using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Sockseek.Server;

public sealed class ServerSessionTokenProvider
{
    public const string AuthorizationHeaderName = "Authorization";
    public const string AuthorizationScheme = "Bearer";

    public ServerSessionTokenProvider(IOptions<ServerOptions> options)
    {
        Token = string.IsNullOrWhiteSpace(options.Value.SessionToken)
            ? GenerateToken()
            : options.Value.SessionToken!;
    }

    public string Token { get; }

    public bool Matches(string? authorizationHeaderValue)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeaderValue))
            return false;

        const StringComparison comparison = StringComparison.Ordinal;
        string prefix = AuthorizationScheme + " ";
        if (!authorizationHeaderValue.StartsWith(prefix, comparison))
            return false;

        string candidate = authorizationHeaderValue[prefix.Length..].Trim();
        if (candidate.Length == 0 || candidate.Length != Token.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(candidate),
            System.Text.Encoding.UTF8.GetBytes(Token));
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }
}
