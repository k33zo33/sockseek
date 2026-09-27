using System.Net;

namespace Sockseek.Integrations.YouTube;

public sealed class YouTubeProviderException : Exception
{
    public YouTubeProviderException(
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
