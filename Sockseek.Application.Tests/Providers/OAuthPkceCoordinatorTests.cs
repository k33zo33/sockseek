using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Common;
using Sockseek.Application.Providers;
using Sockseek.Integrations.Abstractions;

namespace Tests.Application.Providers;

[TestClass]
public sealed class OAuthPkceCoordinatorTests
{
    [TestMethod]
    public void CreateSession_GeneratesPkceAuthorizationRequest()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));
        var coordinator = new OAuthPkceCoordinator(clock);

        var session = coordinator.CreateSession(
            ProviderIds.Spotify,
            new Uri("http://127.0.0.1:48000/oauth/callback"),
            ["playlist-read-private"]);
        var request = session.ToAuthorizationRequest();

        Assert.AreEqual(ProviderIds.Spotify, request.ProviderId);
        Assert.AreEqual("S256", request.CodeChallengeMethod);
        Assert.AreEqual(session.State, request.State);
        Assert.AreEqual(session.CodeChallenge, request.CodeChallenge);
        Assert.AreNotEqual(session.CodeVerifier, session.CodeChallenge);
        Assert.IsTrue(session.CodeVerifier.Length >= 43);
        Assert.IsTrue(session.CodeChallenge.Length >= 43);
        Assert.AreEqual(clock.UtcNow.AddMinutes(10), session.ExpiresAtUtc);
    }

    [TestMethod]
    public void Complete_AcceptsMatchingStateProviderAndRedirect()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));
        var coordinator = new OAuthPkceCoordinator(clock);
        var redirectUri = new Uri("http://127.0.0.1:48000/oauth/callback");
        var session = coordinator.CreateSession(ProviderIds.YouTube, redirectUri, ["youtube.readonly"]);

        var completion = coordinator.Complete(new AuthorizationCallback(
            ProviderIds.YouTube,
            redirectUri,
            session.State,
            "authorization-code",
            null));

        Assert.AreEqual(session.CodeVerifier, completion.CodeVerifier);
        Assert.AreEqual("authorization-code", completion.Callback.Code);
    }

    [TestMethod]
    public void Complete_RejectsStateMismatch()
    {
        var coordinator = new OAuthPkceCoordinator(new FakeClock(DateTimeOffset.UtcNow));
        coordinator.CreateSession(ProviderIds.Spotify, new Uri("http://127.0.0.1:48000/oauth/callback"), []);

        Assert.ThrowsException<OAuthAuthorizationException>(() =>
            coordinator.Complete(new AuthorizationCallback(
                ProviderIds.Spotify,
                new Uri("http://127.0.0.1:48000/oauth/callback"),
                "attacker-state",
                "authorization-code",
                null)));
    }

    [TestMethod]
    public void Complete_RejectsProviderAndRedirectMismatch()
    {
        var coordinator = new OAuthPkceCoordinator(new FakeClock(DateTimeOffset.UtcNow));
        var session = coordinator.CreateSession(ProviderIds.Spotify, new Uri("http://127.0.0.1:48000/oauth/callback"), []);

        Assert.ThrowsException<OAuthAuthorizationException>(() =>
            coordinator.Complete(new AuthorizationCallback(
                ProviderIds.YouTube,
                new Uri("http://127.0.0.1:48000/oauth/callback"),
                session.State,
                "authorization-code",
                null)));

        var secondSession = coordinator.CreateSession(ProviderIds.Spotify, new Uri("http://127.0.0.1:48000/oauth/callback"), []);
        Assert.ThrowsException<OAuthAuthorizationException>(() =>
            coordinator.Complete(new AuthorizationCallback(
                ProviderIds.Spotify,
                new Uri("http://127.0.0.1:48001/oauth/callback"),
                secondSession.State,
                "authorization-code",
                null)));
    }

    [TestMethod]
    public void Complete_RejectsExpiredState()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));
        var coordinator = new OAuthPkceCoordinator(clock, TimeSpan.FromMinutes(1));
        var session = coordinator.CreateSession(ProviderIds.Spotify, new Uri("http://127.0.0.1:48000/oauth/callback"), []);

        clock.UtcNow = clock.UtcNow.AddMinutes(2);

        Assert.ThrowsException<OAuthAuthorizationException>(() =>
            coordinator.Complete(new AuthorizationCallback(
                ProviderIds.Spotify,
                new Uri("http://127.0.0.1:48000/oauth/callback"),
                session.State,
                "authorization-code",
                null)));
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }
}
