using System.Security.Cryptography;
using System.Text;
using Sockseek.Application.Common;
using Sockseek.Integrations.Abstractions;

namespace Sockseek.Application.Providers;

public sealed class OAuthPkceCoordinator(IClock clock, TimeSpan? pendingLifetime = null)
{
    public const string CodeChallengeMethod = "S256";

    private readonly Dictionary<string, OAuthAuthorizationSession> pendingByState = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private readonly TimeSpan lifetime = pendingLifetime ?? TimeSpan.FromMinutes(10);

    public OAuthAuthorizationSession CreateSession(
        string providerId,
        Uri redirectUri,
        IReadOnlyList<string> scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(redirectUri);
        ArgumentNullException.ThrowIfNull(scopes);

        var codeVerifier = CreateRandomBase64Url(byteCount: 32);
        var session = new OAuthAuthorizationSession(
            providerId,
            redirectUri,
            scopes.ToArray(),
            CreateRandomBase64Url(byteCount: 32),
            codeVerifier,
            CreateCodeChallenge(codeVerifier),
            CodeChallengeMethod,
            clock.UtcNow.Add(lifetime));

        lock (gate)
        {
            PruneExpiredLocked();
            pendingByState[session.State] = session;
        }

        return session;
    }

    public OAuthAuthorizationCompletion Complete(AuthorizationCallback callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentException.ThrowIfNullOrWhiteSpace(callback.State);

        OAuthAuthorizationSession session;
        lock (gate)
        {
            PruneExpiredLocked();
            if (!pendingByState.Remove(callback.State, out session!))
                throw new OAuthAuthorizationException("OAuth authorization state is unknown or expired.");
        }

        if (!StringComparer.Ordinal.Equals(session.ProviderId, callback.ProviderId))
            throw new OAuthAuthorizationException("OAuth authorization provider does not match the pending session.");
        if (session.RedirectUri != callback.RedirectUri)
            throw new OAuthAuthorizationException("OAuth redirect URI does not match the pending session.");

        return new OAuthAuthorizationCompletion(session, callback);
    }

    private void PruneExpiredLocked()
    {
        var now = clock.UtcNow;
        foreach (var state in pendingByState
                     .Where(pair => pair.Value.ExpiresAtUtc <= now)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            pendingByState.Remove(state);
        }
    }

    private static string CreateCodeChallenge(string codeVerifier)
        => Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    private static string CreateRandomBase64Url(int byteCount)
    {
        var bytes = new byte[byteCount];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

public sealed record OAuthAuthorizationSession(
    string ProviderId,
    Uri RedirectUri,
    IReadOnlyList<string> Scopes,
    string State,
    string CodeVerifier,
    string CodeChallenge,
    string CodeChallengeMethod,
    DateTimeOffset ExpiresAtUtc)
{
    public AuthorizationRequest ToAuthorizationRequest()
        => new(ProviderId, RedirectUri, Scopes, State, CodeChallenge, CodeChallengeMethod);
}

public sealed record OAuthAuthorizationCompletion(
    OAuthAuthorizationSession Session,
    AuthorizationCallback Callback)
{
    public string CodeVerifier => Session.CodeVerifier;
}

public sealed class OAuthAuthorizationException(string message) : Exception(message);
