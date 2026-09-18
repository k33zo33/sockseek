using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Playback;

namespace Sockseek.Player.Tests;

[TestClass]
public sealed class PlaybackCoordinatorTests
{
    [TestMethod]
    public async Task ResolveCanonicalTrackAsync_DelegatesToSourceResolver()
    {
        var trackId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var resolver = new FakePlaybackSourceResolver(
            PlaybackSourceResolution.LocalFile(trackId, null, fileId, "C:/Music/Track.flac"));
        var coordinator = new PlaybackCoordinator(resolver);

        var result = await coordinator.ResolveCanonicalTrackAsync(trackId);

        Assert.AreEqual(trackId, resolver.CanonicalTrackId);
        Assert.AreEqual(PlaybackSourceKind.LocalFile, result.Kind);
        Assert.AreEqual(fileId, result.LocalMediaFileId);
        Assert.AreEqual("C:/Music/Track.flac", result.Path);
    }

    [TestMethod]
    public async Task ResolvePlaylistItemAsync_DelegatesToSourceResolver()
    {
        var playlistItemId = Guid.NewGuid();
        var resolver = new FakePlaybackSourceResolver(
            PlaybackSourceResolution.PendingResolution(playlistItemId, "pending"));
        var coordinator = new PlaybackCoordinator(resolver);

        var result = await coordinator.ResolvePlaylistItemAsync(playlistItemId);

        Assert.AreEqual(playlistItemId, resolver.PlaylistItemId);
        Assert.AreEqual(PlaybackSourceKind.PendingResolution, result.Kind);
        Assert.AreEqual("pending", result.Reason);
    }

    private sealed class FakePlaybackSourceResolver(PlaybackSourceResolution result) : IPlaybackSourceResolver
    {
        public Guid? CanonicalTrackId { get; private set; }

        public Guid? PlaylistItemId { get; private set; }

        public Task<PlaybackSourceResolution> ResolveCanonicalTrackAsync(
            Guid canonicalTrackId,
            CancellationToken cancellationToken = default)
        {
            CanonicalTrackId = canonicalTrackId;
            return Task.FromResult(result);
        }

        public Task<PlaybackSourceResolution> ResolvePlaylistItemAsync(
            Guid playlistItemId,
            CancellationToken cancellationToken = default)
        {
            PlaylistItemId = playlistItemId;
            return Task.FromResult(result);
        }
    }
}
