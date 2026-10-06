using System.Net;

namespace Sockseek.Integrations.Spotify;

public sealed class SpotifyProviderException : Exception
{
    public SpotifyProviderException(
        string message,
        HttpStatusCode? statusCode = null,
        TimeSpan? retryAfter = null,
        bool reauthorizationRequired = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        RetryAfter = retryAfter;
        ReauthorizationRequired = reauthorizationRequired;
    }

    public HttpStatusCode? StatusCode { get; }

    public TimeSpan? RetryAfter { get; }

    public bool ReauthorizationRequired { get; }
}
