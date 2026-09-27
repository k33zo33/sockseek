using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Integrations.MusicBrainz;

namespace Tests.Application.Providers;

[TestClass]
public sealed class MusicBrainzMetadataClientTests
{
    [TestMethod]
    public async Task LookupRecordingsByIsrcAsync_SendsUserAgentAndMapsRecordingMetadata()
    {
        var clock = new FakeClock();
        var delay = new FakeDelay(clock);
        var handler = new QueueHttpMessageHandler(clock,
        [
            JsonResponse("""
            {
              "isrc": "USRC17607839",
              "recordings": [
                {
                  "id": "2f4a8f0f-1fb5-4f7a-a8f7-9b3f37f91ee2",
                  "title": "Fixture Song",
                  "length": 180123,
                  "isrcs": ["USRC17607839"],
                  "artist-credit": [
                    { "name": "Fixture Artist", "artist": { "name": "Fixture Artist" } }
                  ],
                  "releases": [
                    { "id": "release-1", "title": "Fixture Album" }
                  ]
                }
              ]
            }
            """),
        ]);
        var client = CreateClient(handler, clock, delay);

        var recordings = await client.LookupRecordingsByIsrcAsync("us-rc1-76-07839");

        Assert.AreEqual(1, recordings.Count);
        Assert.AreEqual("2F4A8F0F-1FB5-4F7A-A8F7-9B3F37F91EE2", recordings[0].MusicBrainzRecordingId.ToUpperInvariant());
        Assert.AreEqual("Fixture Song", recordings[0].Title);
        CollectionAssert.AreEqual(new[] { "Fixture Artist" }, recordings[0].Artists.ToArray());
        Assert.AreEqual("Fixture Album", recordings[0].ReleaseTitle);
        Assert.AreEqual(180123, recordings[0].DurationMs);
        CollectionAssert.Contains(recordings[0].Isrcs.ToArray(), "USRC17607839");
        Assert.AreEqual(1, handler.Requests.Count);
        StringAssert.Contains(handler.Requests[0].UserAgent, "Sockseek/1.0");
        StringAssert.Contains(handler.Requests[0].Uri.AbsolutePath, "/ws/2/isrc/USRC17607839");
    }

    [TestMethod]
    public async Task LookupRecordingsByIsrcAsync_SecondCallUsesCacheWithoutNetwork()
    {
        var clock = new FakeClock();
        var delay = new FakeDelay(clock);
        var handler = new QueueHttpMessageHandler(clock,
        [
            JsonResponse("""{ "recordings": [] }"""),
        ]);
        var client = CreateClient(handler, clock, delay);

        await client.LookupRecordingsByIsrcAsync("USRC17607839");
        await client.LookupRecordingsByIsrcAsync("USRC17607839");

        Assert.AreEqual(1, handler.Requests.Count);
        Assert.AreEqual(0, delay.Delays.Count);
    }

    [TestMethod]
    public async Task LookupRecordingsByIsrcAsync_SequentialCacheMissesRespectOneRequestPerSecondLimiter()
    {
        var clock = new FakeClock();
        var delay = new FakeDelay(clock);
        var handler = new QueueHttpMessageHandler(clock,
        [
            JsonResponse("""{ "recordings": [] }"""),
            JsonResponse("""{ "recordings": [] }"""),
            JsonResponse("""{ "recordings": [] }"""),
        ]);
        var client = CreateClient(handler, clock, delay);

        await client.LookupRecordingsByIsrcAsync("USRC17607839");
        await client.LookupRecordingsByIsrcAsync("USRC17607840");
        await client.LookupRecordingsByIsrcAsync("USRC17607841");

        CollectionAssert.AreEqual(
            new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1) },
            delay.Delays.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 27, 10, 0, 1, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 27, 10, 0, 2, TimeSpan.Zero),
            },
            handler.Requests.Select(request => request.SentAtUtc).ToArray());
    }

    [TestMethod]
    public async Task LookupRecordingsByIsrcAsync_ServiceUnavailableRetriesAfterBackoff()
    {
        var clock = new FakeClock();
        var delay = new FakeDelay(clock);
        var handler = new QueueHttpMessageHandler(clock,
        [
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            JsonResponse("""{ "recordings": [] }"""),
        ]);
        var client = CreateClient(handler, clock, delay);

        await client.LookupRecordingsByIsrcAsync("USRC17607839");

        Assert.AreEqual(2, handler.Requests.Count);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(2) }, delay.Delays.ToArray());
    }

    [TestMethod]
    public async Task SearchRecordingsAsync_MapsConservativeFuzzyScoresWithoutAuthoritativeAutoMatch()
    {
        var clock = new FakeClock();
        var delay = new FakeDelay(clock);
        var handler = new QueueHttpMessageHandler(clock,
        [
            JsonResponse("""
            {
              "recordings": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "title": "Fixture Song",
                  "score": 80,
                  "artist-credit": [
                    { "name": "Fixture Artist" }
                  ]
                }
              ]
            }
            """),
        ]);
        var client = CreateClient(handler, clock, delay);

        var results = await client.SearchRecordingsAsync(new MusicBrainzRecordingSearchQuery(
            "Fixture Artist",
            "Fixture Song",
            DurationMs: null));

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("11111111-1111-1111-1111-111111111111", results[0].MusicBrainzRecordingId);
        Assert.AreEqual(0.83d, results[0].ConfidenceScore, 0.001d);
        Assert.IsFalse(results[0].IsHighConfidenceEnrichment);
        StringAssert.Contains(handler.Requests[0].Uri.Query, "recording");
        StringAssert.Contains(handler.Requests[0].Uri.Query, "artist");
    }

    private static MusicBrainzMetadataClient CreateClient(
        HttpMessageHandler handler,
        FakeClock clock,
        FakeDelay delay)
    {
        var limiter = new MusicBrainzRequestLimiter(clock, delay);
        return new MusicBrainzMetadataClient(
            new HttpClient(handler),
            MusicBrainzClientOptions.Default,
            limiter,
            new MusicBrainzMemoryCache(),
            clock,
            delay);
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class QueueHttpMessageHandler(
        FakeClock clock,
        IReadOnlyList<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private int index;

        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri ?? new Uri("about:blank"),
                request.Headers.UserAgent.ToString(),
                clock.UtcNow));

            if (index >= responses.Count)
                throw new InvalidOperationException("No queued MusicBrainz fixture response is available.");

            return Task.FromResult(responses[index++]);
        }
    }

    private sealed record RecordedRequest(Uri Uri, string UserAgent, DateTimeOffset SentAtUtc);

    private sealed class FakeClock : IMusicBrainzClock
    {
        public DateTimeOffset UtcNow { get; private set; } =
            new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan delay)
            => UtcNow = UtcNow.Add(delay);
    }

    private sealed class FakeDelay(FakeClock clock) : IMusicBrainzDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            clock.Advance(delay);
            return Task.CompletedTask;
        }
    }
}
