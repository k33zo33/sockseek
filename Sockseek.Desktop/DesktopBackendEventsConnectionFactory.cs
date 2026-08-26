using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Sockseek.Api;

namespace Sockseek.Desktop;

public static class DesktopBackendEventsConnectionFactory
{
    public static IDesktopEventHubConnection Create(DesktopDaemonHandshake handshake)
    {
        ArgumentNullException.ThrowIfNull(handshake);

        var hubUri = DesktopBackendClientFactory.GetEventsHubUri(handshake);
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Headers["Authorization"] = $"Bearer {handshake.SessionToken}";
                options.Transports = HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .AddJsonProtocol(jsonOptions => SockseekApiJson.ConfigureSerializerOptions(jsonOptions.PayloadSerializerOptions))
            .Build();

        return new SignalRDesktopEventHubConnection(connection);
    }
}
