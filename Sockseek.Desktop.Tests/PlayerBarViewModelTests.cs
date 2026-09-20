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
        Assert.IsTrue(playerBar.UtilityActions[1].IsEnabled);
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

    private static PlayerStateDto CreatePlayerState(string path)
        => new(
            "Playing",
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
                [0]));

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
