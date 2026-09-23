using System.Net;

namespace Sockseek.Integrations.Spotify;

public sealed class SpotifyProviderException : Exception
{
    public SpotifyProviderException(
        string message,
        HttpStatusCode? statusCode = null,
        TimeSpan? retryAfter = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }

    public HttpStatusCode? StatusCode { get; }

    public TimeSpan? RetryAfter { get; }
}
