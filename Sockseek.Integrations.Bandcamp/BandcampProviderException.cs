using System.Net;

namespace Sockseek.Integrations.Bandcamp;

public sealed class BandcampProviderException : Exception
{
    public BandcampProviderException(
        string message,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
