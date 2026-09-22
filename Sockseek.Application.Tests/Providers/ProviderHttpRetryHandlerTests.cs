using System.Net;
using System.Net.Http.Headers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Providers;

namespace Tests.Application.Providers;

[TestClass]
public sealed class ProviderHttpRetryHandlerTests
{
    [TestMethod]
    public async Task SendAsync_TooManyRequests_UsesRetryAfterDelay()
    {
        var delays = new List<TimeSpan>();
        var terminalResponse = new HttpResponseMessage(HttpStatusCode.OK);
        using var client = CreateClient(
            [
                new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3)) },
                },
                terminalResponse,
            ],
            delays);

        using var response = await client.GetAsync("https://provider.example/playlists");

        Assert.AreSame(terminalResponse, response);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(3) }, delays);
    }

    [TestMethod]
    public async Task SendAsync_ServerErrors_UseExponentialBackoff()
    {
        var delays = new List<TimeSpan>();
        using var client = CreateClient(
            [
                new HttpResponseMessage(HttpStatusCode.InternalServerError),
                new HttpResponseMessage(HttpStatusCode.BadGateway),
                new HttpResponseMessage(HttpStatusCode.OK),
            ],
            delays,
            new ProviderHttpRetryOptions(3, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));

        using var response = await client.GetAsync("https://provider.example/me");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        CollectionAssert.AreEqual(
            new[] { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200) },
            delays);
    }

    [TestMethod]
    public async Task SendAsync_StopsAtMaxRetries()
    {
        var delays = new List<TimeSpan>();
        var terminalResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        using var client = CreateClient(
            [
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                terminalResponse,
                new HttpResponseMessage(HttpStatusCode.OK),
            ],
            delays,
            new ProviderHttpRetryOptions(1, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));

        using var response = await client.GetAsync("https://provider.example/me");

        Assert.AreSame(terminalResponse, response);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(100) }, delays);
    }

    [TestMethod]
    public async Task SendAsync_RetriesRequestWithContent()
    {
        var bodies = new List<string>();
        var handler = new QueueHandler(
            [
                new HttpResponseMessage(HttpStatusCode.InternalServerError),
                new HttpResponseMessage(HttpStatusCode.OK),
            ],
            bodies);
        var retryHandler = new ProviderHttpRetryHandler(
            new ProviderHttpRetryOptions(1, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(1)),
            (_, _) => Task.CompletedTask)
        {
            InnerHandler = handler,
        };
        using var client = new HttpClient(retryHandler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://provider.example/token")
        {
            Content = new StringContent("grant_type=authorization_code"),
        };

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        CollectionAssert.AreEqual(
            new[] { "grant_type=authorization_code", "grant_type=authorization_code" },
            bodies);
    }

    private static HttpClient CreateClient(
        IReadOnlyList<HttpResponseMessage> responses,
        List<TimeSpan> delays,
        ProviderHttpRetryOptions? options = null)
    {
        var retryHandler = new ProviderHttpRetryHandler(
            options,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            })
        {
            InnerHandler = new QueueHandler(responses, []),
        };
        return new HttpClient(retryHandler);
    }

    private sealed class QueueHandler(
        IReadOnlyList<HttpResponseMessage> responses,
        List<string> bodies) : HttpMessageHandler
    {
        private int index;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content != null)
                bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));

            return responses[index++];
        }
    }
}
