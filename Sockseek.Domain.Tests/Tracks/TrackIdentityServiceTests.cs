using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Tracks;

namespace Sockseek.Domain.Tests.Tracks;

[TestClass]
public class TrackIdentityServiceTests
{
    [TestMethod]
    public void Match_UsesIsrcAsDeterministicAutoMatch()
    {
        var service = new TrackIdentityService();
        var candidate = new CanonicalTrack("Artist", "Track", 180000, isrc: "hr-abc-01");

        var result = service.Match(candidate, new TrackIdentityQuery("Artist", "Track", 179500, Isrc: "HR-ABC-01"));

        Assert.AreEqual(TrackMatchDisposition.AutoMatch, result.Disposition);
        Assert.AreEqual(TrackMatchMethod.Isrc, result.Method);
        Assert.AreEqual(1.0d, result.Score);
    }

    [TestMethod]
    public void Match_UsesMusicBrainzRecordingIdAsAutoMatch()
    {
        var service = new TrackIdentityService();
        var candidate = new CanonicalTrack("Artist", "Track", 180000, musicBrainzRecordingId: "mbid-1");

        var result = service.Match(candidate, new TrackIdentityQuery("Artist", "Track", MusicBrainzRecordingId: "MBID-1"));

        Assert.AreEqual(TrackMatchDisposition.AutoMatch, result.Disposition);
        Assert.AreEqual(TrackMatchMethod.MusicBrainzRecordingId, result.Method);
    }

    [TestMethod]
    public void Match_UsesPreviousSourceMappingAsAutoMatch()
    {
        var service = new TrackIdentityService();
        var candidate = new CanonicalTrack("Artist", "Track", 180000);
        candidate.AddSource(ExternalProvider.Spotify, "spotify:track:1", null, null);

        var result = service.Match(candidate, new TrackIdentityQuery(
            "Someone Else",
            "Different Title",
            SourceProvider: ExternalProvider.Spotify,
            SourceExternalId: "spotify:track:1"));

        Assert.AreEqual(TrackMatchDisposition.AutoMatch, result.Disposition);
        Assert.AreEqual(TrackMatchMethod.PreviousSourceMapping, result.Method);
        Assert.AreEqual(1.0d, result.Score);
    }

    [TestMethod]
    public void Match_UsesNormalizedArtistTitleDurationAsAutoMatch()
    {
        var service = new TrackIdentityService();
        var candidate = new CanonicalTrack("Artist feat. Guest", "Track (Original Mix)", 180000);

        var result = service.Match(candidate, new TrackIdentityQuery("artist feat guest", "track original mix", 182000));

        Assert.AreEqual(TrackMatchDisposition.AutoMatch, result.Disposition);
        Assert.AreEqual(TrackMatchMethod.NormalizedArtistTitleDuration, result.Method);
        Assert.AreEqual(1.0d, result.Score, 0.0001d);
    }

    [TestMethod]
    public void Match_UsesArtistTitleWithoutDurationAsReviewRequired()
    {
        var service = new TrackIdentityService();
        var candidate = new CanonicalTrack("Artist", "Track", 180000);

        var result = service.Match(candidate, new TrackIdentityQuery("Artist", "Track"));

        Assert.AreEqual(TrackMatchDisposition.ReviewRequired, result.Disposition);
        Assert.AreEqual(TrackMatchMethod.NormalizedArtistTitle, result.Method);
        Assert.AreEqual(0.88d, result.Score, 0.0001d);
    }

    [DataTestMethod]
    [DataRow("Artist feat. Guest", "Track", "artist ft guest", "track")]
    [DataRow("Artist", "Track (Original Mix)", "artist", "track original mix")]
    [DataRow("Artist", "Track - Radio Edit", "artist", "track radio edit")]
    public void Match_FixtureVariants_AutoMatchCompatibleNormalizedForms(string candidateArtist, string candidateTitle, string queryArtist, string queryTitle)
    {
        var service = new TrackIdentityService();
        var candidate = new CanonicalTrack(candidateArtist, candidateTitle, 180000);

        var result = service.Match(candidate, new TrackIdentityQuery(queryArtist, queryTitle, 180500));

        Assert.AreEqual(TrackMatchDisposition.AutoMatch, result.Disposition);
        Assert.AreEqual(TrackMatchMethod.NormalizedArtistTitleDuration, result.Method);
        Assert.IsTrue(result.Score >= 0.92d);
    }

    [DataTestMethod]
    [DataRow("Track Live", "Track")]
    [DataRow("Track Remix", "Track")]
    [DataRow("Track Acoustic", "Track")]
    public void Match_FixtureVariants_VersionConflictPreventsAutoMatch(string candidateTitle, string queryTitle)
    {
        var service = new TrackIdentityService();
        var candidate = new CanonicalTrack("Artist", candidateTitle, 180000);

        var result = service.Match(candidate, new TrackIdentityQuery("Artist", queryTitle, 180000));

        Assert.AreEqual(TrackMatchDisposition.NoMatch, result.Disposition);
        Assert.AreEqual(TrackMatchMethod.None, result.Method);
    }

    [TestMethod]
    public void Match_UsesConfiguredThresholdsForBorderlineNormalizedMatch()
    {
        var service = new TrackIdentityService(new TrackIdentityOptions
        {
            AutoMatchThreshold = 1.01d,
            ReviewThreshold = 0.80d,
            DurationToleranceMs = 10_000,
        });
        var candidate = new CanonicalTrack("Artist", "Track", 180000);

        var result = service.Match(candidate, new TrackIdentityQuery("Artist", "Track", 180000));

        Assert.AreEqual(TrackMatchDisposition.ReviewRequired, result.Disposition);
        Assert.AreEqual(TrackMatchMethod.NormalizedArtistTitleDuration, result.Method);
        Assert.AreEqual(1.0d, result.Score, 0.0001d);
    }

    [TestMethod]
    public void Match_ReturnsNoMatchWhenDurationOutsideToleranceForIsrc()
    {
        var service = new TrackIdentityService();
        var candidate = new CanonicalTrack("Artist", "Track", 180000, isrc: "HR-ABC-01");

        var result = service.Match(candidate, new TrackIdentityQuery("Artist", "Track", 250000, Isrc: "HR-ABC-01"));

        Assert.AreEqual(TrackMatchDisposition.NoMatch, result.Disposition);
        Assert.AreEqual(TrackMatchMethod.None, result.Method);
    }

    [TestMethod]
    public void CanonicalTrack_PreventsDuplicateSourceAndLocalMediaEntries()
    {
        var candidate = new CanonicalTrack("Artist", "Track", 180000);
        candidate.AddSource(ExternalProvider.Spotify, "spotify:track:1", null, null);
        candidate.AddLocalMediaFile("C:\\Music\\Artist\\Track.mp3", 100, DateTimeOffset.UtcNow, 180000, "mp3", 320, 44100, 16, LocalMediaAvailability.Available);

        Assert.ThrowsException<InvalidOperationException>(() => candidate.AddSource(ExternalProvider.Spotify, "spotify:track:1", null, null));
        Assert.ThrowsException<InvalidOperationException>(() => candidate.AddLocalMediaFile("C:/Music/Artist/Track.mp3", 100, DateTimeOffset.UtcNow, 180000, "mp3", 320, 44100, 16, LocalMediaAvailability.Available));
    }
}
