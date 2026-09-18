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

    [TestMethod]
    public async Task PlayCanonicalTrackAsync_LocalFile_LoadsAndStartsEngine()
    {
        var trackId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var engine = new FakeMediaEngine();
        var coordinator = new PlaybackCoordinator(
            new FakePlaybackSourceResolver(PlaybackSourceResolution.LocalFile(trackId, null, fileId, "C:/Music/Track.flac")),
            engine);

        var result = await coordinator.PlayCanonicalTrackAsync(trackId);

        Assert.AreEqual(PlaybackState.Playing, result.State);
        Assert.AreEqual(trackId, result.CanonicalTrackId);
        Assert.AreEqual(fileId, result.LocalMediaFileId);
        CollectionAssert.AreEqual(new[] { "Load:C:/Music/Track.flac", "Play" }, engine.Calls.ToArray());
    }

    [TestMethod]
    public async Task PlayPlaylistItemAsync_PendingResolution_FailsWithoutOpeningEngine()
    {
        var playlistItemId = Guid.NewGuid();
        var engine = new FakeMediaEngine();
        var coordinator = new PlaybackCoordinator(
            new FakePlaybackSourceResolver(PlaybackSourceResolution.PendingResolution(playlistItemId, "not ready")),
            engine);

        var result = await coordinator.PlayPlaylistItemAsync(playlistItemId);

        Assert.AreEqual(PlaybackState.Failed, result.State);
        Assert.AreEqual(playlistItemId, result.PlaylistItemId);
        Assert.AreEqual("not ready", result.ErrorMessage);
        Assert.AreEqual(0, engine.Calls.Count);
    }

    [TestMethod]
    public async Task PlayCanonicalTrackAsync_ProviderUri_FailsBeforeOpeningEngine()
    {
        var trackId = Guid.NewGuid();
        var engine = new FakeMediaEngine();
        var coordinator = new PlaybackCoordinator(
            new FakePlaybackSourceResolver(PlaybackSourceResolution.LocalFile(trackId, null, Guid.NewGuid(), "https://provider.example/audio.mp3")),
            engine);

        var result = await coordinator.PlayCanonicalTrackAsync(trackId);

        Assert.AreEqual(PlaybackState.Failed, result.State);
        Assert.AreEqual("Playback source must be a local file path.", result.ErrorMessage);
        Assert.AreEqual(0, engine.Calls.Count);
    }

    [TestMethod]
    public async Task PlayCanonicalTrackAsync_EngineFailure_EntersFailedStateWithoutThrowing()
    {
        var trackId = Guid.NewGuid();
        var engine = new FakeMediaEngine { LoadException = new InvalidDataException("bad file") };
        var coordinator = new PlaybackCoordinator(
            new FakePlaybackSourceResolver(PlaybackSourceResolution.LocalFile(trackId, null, Guid.NewGuid(), "C:/Music/Broken.mp3")),
            engine);

        var result = await coordinator.PlayCanonicalTrackAsync(trackId);

        Assert.AreEqual(PlaybackState.Failed, result.State);
        Assert.AreEqual("bad file", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "Load:C:/Music/Broken.mp3" }, engine.Calls.ToArray());
    }

    [TestMethod]
    public async Task PauseResumeAndStopAsync_UpdateStateAndEngine()
    {
        var trackId = Guid.NewGuid();
        var engine = new FakeMediaEngine();
        var coordinator = new PlaybackCoordinator(
            new FakePlaybackSourceResolver(PlaybackSourceResolution.LocalFile(trackId, null, Guid.NewGuid(), "C:/Music/Track.mp3")),
            engine);

        await coordinator.PlayCanonicalTrackAsync(trackId);
        var paused = await coordinator.PauseAsync();
        var resumed = await coordinator.ResumeAsync();
        var stopped = await coordinator.StopAsync();

        Assert.AreEqual(PlaybackState.Paused, paused.State);
        Assert.AreEqual(PlaybackState.Playing, resumed.State);
        Assert.AreEqual(PlaybackState.Stopped, stopped.State);
        CollectionAssert.AreEqual(
            new[] { "Load:C:/Music/Track.mp3", "Play", "Pause", "Play", "Stop" },
            engine.Calls.ToArray());
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

    private sealed class FakeMediaEngine : IMediaEngine
    {
        public List<string> Calls { get; } = [];

        public Exception? LoadException { get; init; }

        public Task LoadAsync(string path, CancellationToken cancellationToken = default)
        {
            Calls.Add("Load:" + path);
            if (LoadException != null)
                throw LoadException;

            return Task.CompletedTask;
        }

        public Task PlayAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Play");
            return Task.CompletedTask;
        }

        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Pause");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Stop");
            return Task.CompletedTask;
        }
    }
}
