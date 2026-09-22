using System.Net;

namespace Sockseek.Application.Providers;

public sealed class ProviderHttpRetryHandler : DelegatingHandler
{
    private readonly ProviderHttpRetryOptions options;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;

    public ProviderHttpRetryHandler(
        ProviderHttpRetryOptions? options = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.options = (options ?? ProviderHttpRetryOptions.Default).Validate();
        this.delay = delay ?? Task.Delay;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var attempts = 0;
        while (true)
        {
            var requestAttempt = attempts == 0
                ? request
                : await CloneRequestAsync(request, cancellationToken);
            var response = await base.SendAsync(requestAttempt, cancellationToken);
            if (attempts > 0)
                requestAttempt.Dispose();
            if (!ShouldRetry(response.StatusCode) || attempts >= options.MaxRetries)
                return response;

            var retryDelay = GetRetryDelay(response, attempts);
            response.Dispose();
            attempts++;
            await delay(retryDelay, cancellationToken);
        }
    }

    private bool ShouldRetry(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests
            || statusCode == HttpStatusCode.RequestTimeout
            || (int)statusCode >= 500;

    private TimeSpan GetRetryDelay(HttpResponseMessage response, int failedAttemptIndex)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
            return Cap(delta);

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var delayUntilDate = date - DateTimeOffset.UtcNow;
            if (delayUntilDate > TimeSpan.Zero)
                return Cap(delayUntilDate);
        }

        var backoff = TimeSpan.FromMilliseconds(
            options.BaseDelay.TotalMilliseconds * Math.Pow(2, failedAttemptIndex));
        return Cap(backoff);
    }

    private TimeSpan Cap(TimeSpan delay)
        => delay <= options.MaxDelay ? delay : options.MaxDelay;

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (request.Content != null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(contentBytes);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}

public sealed record ProviderHttpRetryOptions(
    int MaxRetries,
    TimeSpan BaseDelay,
    TimeSpan MaxDelay)
{
    public static ProviderHttpRetryOptions Default { get; } = new(
        MaxRetries: 3,
        BaseDelay: TimeSpan.FromMilliseconds(250),
        MaxDelay: TimeSpan.FromSeconds(8));

    public ProviderHttpRetryOptions Validate()
    {
        if (MaxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxRetries), "Retry count cannot be negative.");
        if (BaseDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(BaseDelay), "Base delay must be positive.");
        if (MaxDelay < BaseDelay)
            throw new ArgumentOutOfRangeException(nameof(MaxDelay), "Max delay must be greater than or equal to base delay.");
        return this;
    }
}
