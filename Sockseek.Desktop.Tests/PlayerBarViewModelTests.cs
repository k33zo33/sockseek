using System.Net;
using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class PlayerBarViewModelTests
{
    [TestMethod]
    public async Task ConnectAsync_LoadsPlayerStateFromApiClient()
    {
        var handler = new StubHttpMessageHandler(_ => CreatePlayerState("C:/Music/Artist/Track.mp3"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var playerBar = new PlayerBarPlaceholderViewModel();

        await playerBar.ConnectAsync(new SockseekApiClient(http));

        Assert.AreEqual("Track.mp3", playerBar.Title);
        Assert.AreEqual("Local player ready", playerBar.Artist);
        Assert.AreEqual("00:42 / --:--", playerBar.Progress);
        Assert.AreEqual("1 queued - All", playerBar.QueueSummary);
        Assert.AreEqual("Volume 70%", playerBar.VolumeHint);
        Assert.IsTrue(playerBar.CanGoPrevious);
        Assert.IsTrue(playerBar.CanPlayPause);
        Assert.IsTrue(playerBar.CanGoNext);
        Assert.IsTrue(playerBar.TransportActions.All(action => action.IsEnabled));
        Assert.IsTrue(playerBar.UtilityActions.All(action => action.IsEnabled));
        Assert.IsTrue(playerBar.HasQueueItems);
        Assert.IsFalse(playerBar.IsQueueEmpty);
        Assert.AreEqual("Player queue (1)", playerBar.ExpandedQueueTitle);
        Assert.AreEqual("1", playerBar.QueueItems[0].PositionLabel);
        Assert.AreEqual("Track.mp3", playerBar.QueueItems[0].Title);
        Assert.IsTrue(playerBar.QueueItems[0].IsCurrent);
    }

    [TestMethod]
    public async Task Disconnect_IgnoresLateRefreshResult()
    {
        var releaseResponse = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(async (_, _) =>
        {
            await releaseResponse.Task;
            return StubHttpMessageHandler.CreateResponse(CreatePlayerState("C:/Music/Artist/Late.mp3"));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var playerBar = new PlayerBarPlaceholderViewModel();

        var connectTask = playerBar.ConnectAsync(new SockseekApiClient(http));
        playerBar.Disconnect();
        releaseResponse.SetResult(true);
        await connectTask;

        Assert.AreEqual("Nothing playing", playerBar.Title);
        Assert.AreEqual("Choose a local track or completed download", playerBar.Artist);
        Assert.AreEqual("00:00 / --:--", playerBar.Progress);
        Assert.AreEqual("Queue unavailable until playback coordinator is connected", playerBar.QueueSummary);
        Assert.IsFalse(playerBar.CanPlayPause);
    }

    [TestMethod]
    public async Task ConnectAsync_UsesNowPlayingMetadataWhenAvailable()
    {
        var nowPlaying = new PlayerNowPlayingDto(
            "Tagged Title",
            "Tagged Artist",
            "Tagged Album",
            185000,
            "flac",
            null,
            "local_media_file");
        var handler = new StubHttpMessageHandler(_ => CreatePlayerState("C:/Music/Artist/Track.flac", nowPlaying: nowPlaying));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var playerBar = new PlayerBarPlaceholderViewModel();

        await playerBar.ConnectAsync(new SockseekApiClient(http));

        Assert.AreEqual("Tagged Title", playerBar.Title);
        Assert.AreEqual("Tagged Artist - Tagged Album", playerBar.Artist);
        Assert.AreEqual("00:42 / 03:05", playerBar.Progress);
        Assert.AreEqual("Tagged Title", playerBar.QueueItems[0].Title);
    }

    [TestMethod]
    public async Task ConnectAsync_BufferingStateShowsBufferStatus()
    {
        var buffer = new PlayerBufferDto(
            "Buffering",
            false,
            BufferedUntilMs: 20_000,
            SeekLimitMs: 20_000,
            AvailableBytes: 320_000,
            ExpectedBytes: 960_000,
            DownloadBytesPerSecond: 98_304,
            "Buffered data is below resume threshold.");
        var handler = new StubHttpMessageHandler(_ => CreatePlayerState(
            "C:/Music/Artist/Track.mp3.incomplete",
            state: "Buffering",
            buffer: buffer));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var playerBar = new PlayerBarPlaceholderViewModel();

        await playerBar.ConnectAsync(new SockseekApiClient(http));

        Assert.IsTrue(playerBar.HasBufferStatus);
        Assert.AreEqual("Buffering to 00:20 - seek to 00:20 - 96.0 KB/s", playerBar.BufferStatus);
        Assert.IsFalse(playerBar.CanPlayPause);
    }

    [TestMethod]
    public async Task TryHandleInput_PlayPauseWhenPlaying_PostsPauseCommand()
    {
        var pauseRequestPath = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
                return Task.FromResult(StubHttpMessageHandler.CreateResponse(CreatePlayerState("C:/Music/Artist/Track.mp3")));

            pauseRequestPath.SetResult(request.RequestUri?.AbsolutePath ?? string.Empty);
            return Task.FromResult(StubHttpMessageHandler.CreateResponse(CreatePlayerState(
                "C:/Music/Artist/Track.mp3",
                state: "Paused")));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var playerBar = new PlayerBarPlaceholderViewModel();
        await playerBar.ConnectAsync(new SockseekApiClient(http));

        var handled = playerBar.TryHandleInput(DesktopPlayerInput.TogglePlayPause);

        Assert.IsTrue(handled);
        Assert.AreEqual("/api/v1/player/pause", await pauseRequestPath.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [TestMethod]
    public void TryHandleInput_DisabledAction_ReturnsFalse()
    {
        var playerBar = new PlayerBarPlaceholderViewModel();

        Assert.IsFalse(playerBar.TryHandleInput(DesktopPlayerInput.TogglePlayPause));
    }

    [TestMethod]
    public void ApplyState_UpdatesVisiblePlayerState()
    {
        var buffer = new PlayerBufferDto(
            "WaitingForInitialBuffer",
            false,
            BufferedUntilMs: 10_000,
            SeekLimitMs: 10_000,
            AvailableBytes: 160_000,
            ExpectedBytes: 960_000,
            DownloadBytesPerSecond: null,
            "Initial buffer threshold has not been reached.");
        var playerBar = new PlayerBarPlaceholderViewModel();

        playerBar.ApplyState(CreatePlayerState(
            "C:/Music/Artist/Track.mp3.incomplete",
            state: "Buffering",
            buffer: buffer));

        Assert.AreEqual("Track.mp3.incomplete", playerBar.Title);
        Assert.AreEqual("Buffering to 00:10 - seek to 00:10", playerBar.BufferStatus);
        Assert.IsTrue(playerBar.HasBufferStatus);
    }

    [TestMethod]
    public async Task QueueUtilityAction_TogglesExpandedQueue()
    {
        var handler = new StubHttpMessageHandler(_ => CreatePlayerState("C:/Music/Artist/Track.mp3"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var playerBar = new PlayerBarPlaceholderViewModel();
        await playerBar.ConnectAsync(new SockseekApiClient(http));

        playerBar.UtilityActions[0].Command.Execute(null);

        Assert.IsTrue(playerBar.IsQueueExpanded);

        playerBar.UtilityActions[2].Command.Execute(null);

        Assert.IsFalse(playerBar.IsQueueExpanded);
    }

    [TestMethod]
    public void Disconnect_RestoresPlaceholderState()
    {
        var playerBar = new PlayerBarPlaceholderViewModel();

        playerBar.Disconnect();

        Assert.AreEqual("Nothing playing", playerBar.Title);
        Assert.AreEqual("Choose a local track or completed download", playerBar.Artist);
        Assert.AreEqual("00:00 / --:--", playerBar.Progress);
        Assert.AreEqual("Queue unavailable until playback coordinator is connected", playerBar.QueueSummary);
        Assert.IsFalse(playerBar.CanPlayPause);
    }

    private static PlayerStateDto CreatePlayerState(
        string path,
        string state = "Playing",
        PlayerNowPlayingDto? nowPlaying = null,
        PlayerBufferDto? buffer = null)
        => new(
            state,
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            path,
            null,
            42000,
            0.7,
            false,
            new PlayerQueueDto(
                [new PlayerQueueItemDto(Guid.NewGuid(), Guid.NewGuid(), null, null)],
                0,
                "All",
                false,
                0,
                [0]),
            nowPlaying,
            buffer);

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, PlayerStateDto> responseFactory)
            : this((request, _) => Task.FromResult(CreateResponse(responseFactory(request))))
        {
        }

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
            => this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => responseFactory(request, cancellationToken);

        public static HttpResponseMessage CreateResponse(PlayerStateDto response)
            => new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response, options: SockseekApiJson.CreateSerializerOptions()),
            };
    }
}
