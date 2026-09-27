using Sockseek.Core.Settings;
using Sockseek.Api;
using Sockseek.Application.Security;
using Soulseek;

namespace Sockseek.Server;

public sealed class ServerOptions
{
    public string Name { get; set; } = "Sockseek";
    public EngineSettings Engine { get; set; } = new();
    public DownloadSettings DefaultDownload { get; set; } = new();
    public DownloadSettingsPatchDto? LaunchDownloadSettings { get; set; }
    public ProfileCatalog Profiles { get; set; } = ProfileCatalog.Empty;
    public string? ConfigDir { get; set; }
    public string? DatabasePath { get; set; }
    public string? DatabaseBackupDir { get; set; }
    public string? SecretStoreDir { get; set; }
    public Func<EngineSettings, ISoulseekClient>? ClientFactory { get; set; }
    public Func<ISecretStore>? SecretStoreFactory { get; set; }
    public string? SessionToken { get; set; }
    public bool ExperimentalProgressivePlayback { get; set; }
    public BandcampServerOptions Bandcamp { get; set; } = new();
    public SpotifyServerOptions Spotify { get; set; } = new();
    public YouTubeServerOptions YouTube { get; set; } = new();
}

public sealed class BandcampServerOptions
{
    public Func<HttpMessageHandler>? HttpMessageHandlerFactory { get; set; }
}

public sealed class SpotifyServerOptions
{
    public string? ClientId { get; set; }
    public string AccountsBaseUri { get; set; } = "https://accounts.spotify.com/";
    public string ApiBaseUri { get; set; } = "https://api.spotify.com/v1/";
    public Func<HttpMessageHandler>? HttpMessageHandlerFactory { get; set; }
}

public sealed class YouTubeServerOptions
{
    public string? ClientId { get; set; }
    public string AuthorizationEndpointUri { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";
    public string TokenEndpointUri { get; set; } = "https://oauth2.googleapis.com/token";
    public string ApiBaseUri { get; set; } = "https://www.googleapis.com/youtube/v3/";
    public Func<HttpMessageHandler>? HttpMessageHandlerFactory { get; set; }
}
