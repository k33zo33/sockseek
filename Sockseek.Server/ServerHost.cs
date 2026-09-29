using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sockseek.Application.Common;
using Sockseek.Application.Providers;
using Sockseek.Application.Security;
using Sockseek.Api;
using Sockseek.Application.Playback;
using Sockseek.Application.Soulseek;
using Sockseek.Domain.Accounts;
using Sockseek.Domain.Playlists;
using Sockseek.Domain.Tracks;
using Sockseek.Domain.Workflows;
using Sockseek.Infrastructure;
using Sockseek.Infrastructure.LocalLibrary;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;
using Sockseek.Infrastructure.Security;
using Sockseek.Integrations.Abstractions;
using Sockseek.Integrations.Bandcamp;
using Sockseek.Integrations.Spotify;
using Sockseek.Integrations.YouTube;
using Sockseek.Player;

namespace Sockseek.Server;

public static class ServerHost
{
    public const string CorrelationIdHeaderName = "X-Correlation-Id";
    public const string DefaultListenUrl = "http://127.0.0.1:5030";

    public static WebApplication Build(string[] args, ServerOptions? options = null, string? url = null, TextWriter? startupHandshakeWriter = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(consoleOptions =>
        {
            consoleOptions.SingleLine = false;
            consoleOptions.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

        builder.WebHost.UseUrls(ResolveListenUrl(url, builder.Configuration[WebHostDefaults.ServerUrlsKey]));

        if (options != null)
            builder.Services.AddSingleton<IOptions<ServerOptions>>(Options.Create(options));
        else
            builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection("SockseekServer"));

        builder.Services.Configure<JsonOptions>(jsonOptions =>
        {
            jsonOptions.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            SockseekApiJson.ConfigureSerializerOptions(jsonOptions.SerializerOptions);
        });

        builder.Services.AddSignalR()
            .AddJsonProtocol(jsonOptions =>
            {
                jsonOptions.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                SockseekApiJson.ConfigureSerializerOptions(jsonOptions.PayloadSerializerOptions);
            });
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new()
                {
                    Title = "Sockseek daemon API",
                    Version = GetOpenApiVersion(),
                    Description = "HTTP API for the Sockseek daemon."
                };

                return Task.CompletedTask;
            });
        });
        builder.Services.AddSingleton<EngineSupervisor>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<EngineSupervisor>().StateStore);
        builder.Services.AddSingleton<ISoulseekEngineGateway, ServerSoulseekEngineGateway>();
        builder.Services.AddSingleton<ServerSessionTokenProvider>();
        builder.Services.AddSingleton(ProviderCapabilityRegistry.CreateDefault());
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<TrackIdentityService>();
        builder.Services.AddSingleton<OAuthPkceCoordinator>();
        builder.Services.AddSingleton<ISecretStore>(sp =>
        {
            var serverOptions = sp.GetRequiredService<IOptions<ServerOptions>>().Value;
            return serverOptions.SecretStoreFactory?.Invoke()
                ?? new WindowsDpapiSecretStore(ResolveSecretStoreDirectory(serverOptions));
        });
        builder.Services.AddSingleton(sp => CreateBandcampProvider(sp));
        builder.Services.AddSingleton<IPlaylistSourceProvider>(sp => sp.GetRequiredService<BandcampPlaylistSourceProvider>());
        builder.Services.AddSingleton(sp => CreateSpotifyProvider(sp));
        builder.Services.AddSingleton<IPlaylistSourceProvider>(sp => sp.GetRequiredService<SpotifyPlaylistSourceProvider>());
        builder.Services.AddSingleton(sp => CreateYouTubeProvider(sp));
        builder.Services.AddSingleton<IPlaylistSourceProvider>(sp => sp.GetRequiredService<YouTubePlaylistSourceProvider>());
        builder.Services.AddSingleton<ServerEventBroadcaster>();
        builder.Services.AddSingleton<ServerActivityLogReporter>();
        builder.Services.AddSingleton<ServerDatabaseMigrationService>();
        builder.Services.AddSingleton<LocalLibraryEndpointService>();
        builder.Services.AddSingleton(sp =>
            new LocalArtworkCache(ResolveArtworkCacheDirectory(sp.GetRequiredService<IOptions<ServerOptions>>().Value)));
        builder.Services.AddDbContext<SockseekDbContext>((sp, db) =>
        {
            var serverOptions = sp.GetRequiredService<IOptions<ServerOptions>>().Value;
            var databasePath = ResolveDatabasePath(serverOptions);
            EnsureParentDirectoryExists(databasePath);
            db.UseSqlite($"Data Source={databasePath}");
        });
        builder.Services.AddScoped<ExternalAccountStore>();
        builder.Services.AddScoped<ExternalPlaylistSnapshotStore>();
        builder.Services.AddScoped<PlaylistQueryStore>();
        builder.Services.AddScoped<PlaylistLocalMatchResolver>();
        builder.Services.AddScoped<PlaylistDownloadOrchestrator>();
        builder.Services.AddScoped<PlaylistWorkflowSyncService>();
        builder.Services.AddScoped<CanonicalTrackStore>();
        builder.Services.AddScoped<LocalPlaybackSourceResolver>();
        builder.Services.AddSingleton<IPlaybackSourceResolver, ScopedPlaybackSourceResolver>();
        builder.Services.AddSingleton<IMediaEngine, LibVlcMediaEngine>();
        builder.Services.AddSingleton<PlaybackCoordinator>();
        builder.Services.AddSingleton<PlaybackQueuePersistenceService>();
        builder.Services.AddSingleton<PlaylistWorkflowRecoveryService>();
        if (!IsOpenApiGenerationProcess())
        {
            builder.Services.AddHostedService<EngineRuntimeHostedService>();
            builder.Services.AddHostedService<LocalLibraryBackgroundScanHostedService>();
        }

        var app = builder.Build();
        DesktopDaemonStartupHandshakeEmitter.Register(app, startupHandshakeWriter ?? Console.Out);
        CoreLoggerBridge.Configure(app.Services, (options ?? app.Services.GetRequiredService<IOptions<ServerOptions>>().Value).Engine.LogLevel);
        _ = app.Services.GetRequiredService<ServerEventBroadcaster>();
        _ = app.Services.GetRequiredService<ServerActivityLogReporter>();

        app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var incoming)
                && !string.IsNullOrWhiteSpace(incoming)
                ? incoming.ToString()
                : Guid.NewGuid().ToString("n");

            context.TraceIdentifier = correlationId;
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            await next();
        });

        app.UseExceptionHandler(v1Errors => v1Errors.Run(context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var correlationId = GetCorrelationId(context);
            if (feature?.Error != null)
                Sockseek.Core.SockseekLog.Daemon.Error(feature.Error, $"Unhandled server error ({correlationId})");

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;

            if (context.Request.Path.StartsWithSegments("/api/v1"))
            {
                return context.Response.WriteAsJsonAsync(new AppErrorDto(
                    Code: "internal_error",
                    Message: "An unexpected server error occurred.",
                    CorrelationId: correlationId));
            }

            return context.Response.WriteAsJsonAsync(new ApiErrorDto($"Unexpected server error. CorrelationId: {correlationId}"));
        }));

        app.Use(async (context, next) =>
        {
            if (!RequiresSessionToken(context.Request.Path))
            {
                await next();
                return;
            }

            var sessionTokens = context.RequestServices.GetRequiredService<ServerSessionTokenProvider>();
            var authorization = context.Request.Headers[HeaderNames.Authorization].ToString();
            if (sessionTokens.Matches(authorization))
            {
                await next();
                return;
            }

            var correlationId = GetCorrelationId(context);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            context.Response.Headers[HeaderNames.WWWAuthenticate] = ServerSessionTokenProvider.AuthorizationScheme;
            await context.Response.WriteAsJsonAsync(new AppErrorDto(
                Code: "unauthorized",
                Message: "A valid local session token is required for this API endpoint.",
                CorrelationId: correlationId));
        });

        app.MapOpenApi("/api/openapi.json");
        MapEndpoints(app);
        return app;
    }

    public static string ResolveListenUrl(string? url, string? configuredUrl = null)
        => !string.IsNullOrWhiteSpace(url)
            ? url
            : !string.IsNullOrWhiteSpace(configuredUrl)
                ? configuredUrl
                : DefaultListenUrl;

    private static string ResolveDatabasePath(ServerOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DatabasePath))
            return Path.GetFullPath(options.DatabasePath);

        string baseDirectory = !string.IsNullOrWhiteSpace(options.ConfigDir)
            ? options.ConfigDir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sockseek");
        return Path.GetFullPath(Path.Combine(baseDirectory, "sockseek.db"));
    }

    private static string ResolveArtworkCacheDirectory(ServerOptions options)
    {
        string baseDirectory = !string.IsNullOrWhiteSpace(options.ConfigDir)
            ? options.ConfigDir
            : Path.GetDirectoryName(ResolveDatabasePath(options))
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sockseek");
        return Path.GetFullPath(Path.Combine(baseDirectory, "artwork-cache"));
    }

    private static string ResolveSecretStoreDirectory(ServerOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SecretStoreDir))
            return Path.GetFullPath(options.SecretStoreDir);

        string baseDirectory = !string.IsNullOrWhiteSpace(options.ConfigDir)
            ? options.ConfigDir
            : Path.GetDirectoryName(ResolveDatabasePath(options))
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sockseek");
        return Path.GetFullPath(Path.Combine(baseDirectory, "secrets"));
    }

    private static void EnsureParentDirectoryExists(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    private static SpotifyPlaylistSourceProvider CreateSpotifyProvider(IServiceProvider services)
    {
        var serverOptions = services.GetRequiredService<IOptions<ServerOptions>>().Value;
        var spotify = serverOptions.Spotify;
        var handler = spotify.HttpMessageHandlerFactory?.Invoke();
        var httpClient = handler == null ? new HttpClient() : new HttpClient(handler);
        return new SpotifyPlaylistSourceProvider(
            httpClient,
            services.GetRequiredService<ISecretStore>(),
            new SpotifyPlaylistSourceOptions
            {
                ClientId = string.IsNullOrWhiteSpace(spotify.ClientId) ? "__unconfigured_spotify_client__" : spotify.ClientId,
                AccountsBaseUri = new Uri(spotify.AccountsBaseUri, UriKind.Absolute),
                ApiBaseUri = new Uri(spotify.ApiBaseUri, UriKind.Absolute),
            });
    }

    private static BandcampPlaylistSourceProvider CreateBandcampProvider(IServiceProvider services)
    {
        var serverOptions = services.GetRequiredService<IOptions<ServerOptions>>().Value;
        var handler = serverOptions.Bandcamp.HttpMessageHandlerFactory?.Invoke();
        var httpClient = handler == null ? new HttpClient() : new HttpClient(handler);
        return new BandcampPlaylistSourceProvider(httpClient);
    }

    private static YouTubePlaylistSourceProvider CreateYouTubeProvider(IServiceProvider services)
    {
        var serverOptions = services.GetRequiredService<IOptions<ServerOptions>>().Value;
        var youtube = serverOptions.YouTube;
        var handler = youtube.HttpMessageHandlerFactory?.Invoke();
        var httpClient = handler == null ? new HttpClient() : new HttpClient(handler);
        return new YouTubePlaylistSourceProvider(
            httpClient,
            services.GetRequiredService<ISecretStore>(),
            new YouTubePlaylistSourceOptions
            {
                ClientId = string.IsNullOrWhiteSpace(youtube.ClientId) ? "__unconfigured_youtube_client__" : youtube.ClientId,
                AuthorizationEndpointUri = new Uri(youtube.AuthorizationEndpointUri, UriKind.Absolute),
                TokenEndpointUri = new Uri(youtube.TokenEndpointUri, UriKind.Absolute),
                ApiBaseUri = new Uri(youtube.ApiBaseUri, UriKind.Absolute),
            });
    }

    private static bool IsProviderConfigured(string providerId, ServerOptions options)
        => providerId switch
        {
            ProviderIds.Spotify => !string.IsNullOrWhiteSpace(options.Spotify.ClientId),
            ProviderIds.YouTube => !string.IsNullOrWhiteSpace(options.YouTube.ClientId),
            _ => true,
        };

    private static IReadOnlyList<string> GetProviderDefaultScopes(string providerId)
        => providerId switch
        {
            ProviderIds.Spotify => SpotifyPlaylistSourceOptions.DefaultScopes,
            ProviderIds.YouTube => YouTubePlaylistSourceOptions.DefaultScopes,
            _ => [],
        };

    private static bool TryCreateUri(string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out uri!)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && (uri.IsLoopback || string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal)))
        {
            return true;
        }

        uri = null!;
        return false;
    }

    private static IPlaylistSourceProvider? FindPlaylistProvider(
        IEnumerable<IPlaylistSourceProvider> providers,
        string providerId)
        => providers.FirstOrDefault(provider => StringComparer.Ordinal.Equals(provider.ProviderId, providerId));

    private static void RememberProviderAccount(
        IPlaylistSourceProvider provider,
        ExternalAccountEntity account)
    {
        var snapshot = ToProviderAccountSnapshot(account);
        if (provider is SpotifyPlaylistSourceProvider spotify)
            spotify.RememberAccount(snapshot);
        if (provider is YouTubePlaylistSourceProvider youtube)
            youtube.RememberAccount(snapshot);
    }

    private static ExternalAccountSnapshot ToProviderAccountSnapshot(ExternalAccountEntity account)
        => new(
            new ExternalAccountId(account.Id),
            ToProviderId(account.Provider),
            account.ExternalUserId,
            account.DisplayName,
            account.SecretReference,
            account.LastAuthorizedAtUtc ?? DateTimeOffset.UtcNow);

    private static IResult BadAppRequest(
        HttpContext context,
        string code,
        string message)
        => Results.BadRequest(new AppErrorDto(code, message, GetCorrelationId(context)));

    private static bool IsProviderFacingException(Exception ex)
        => ex is SpotifyProviderException
            or BandcampProviderException
            or YouTubeProviderException
            or OAuthAuthorizationException
            or ArgumentException
            or InvalidOperationException
            or KeyNotFoundException;

    private static IResult ProviderError(HttpContext context, Exception exception)
    {
        var correlationId = GetCorrelationId(context);
        return exception switch
        {
            SpotifyProviderException spotify when spotify.StatusCode == System.Net.HttpStatusCode.Forbidden =>
                Results.Json(new AppErrorDto("provider_forbidden", spotify.Message, correlationId), statusCode: StatusCodes.Status403Forbidden),
            SpotifyProviderException spotify when spotify.StatusCode == System.Net.HttpStatusCode.TooManyRequests =>
                Results.Json(new AppErrorDto("provider_rate_limited", spotify.Message, correlationId), statusCode: StatusCodes.Status429TooManyRequests),
            SpotifyProviderException spotify when spotify.StatusCode == System.Net.HttpStatusCode.Unauthorized =>
                Results.Json(new AppErrorDto("provider_unauthorized", spotify.Message, correlationId), statusCode: StatusCodes.Status401Unauthorized),
            SpotifyProviderException spotify =>
                Results.BadRequest(new AppErrorDto("provider_error", spotify.Message, correlationId)),
            YouTubeProviderException youtube when youtube.ReauthorizationRequired =>
                Results.Json(new AppErrorDto("provider_reauthorization_required", youtube.Message, correlationId), statusCode: StatusCodes.Status401Unauthorized),
            YouTubeProviderException youtube when youtube.StatusCode == System.Net.HttpStatusCode.Forbidden =>
                Results.Json(new AppErrorDto("provider_forbidden", youtube.Message, correlationId), statusCode: StatusCodes.Status403Forbidden),
            YouTubeProviderException youtube when youtube.StatusCode == System.Net.HttpStatusCode.TooManyRequests =>
                Results.Json(new AppErrorDto("provider_rate_limited", youtube.Message, correlationId), statusCode: StatusCodes.Status429TooManyRequests),
            YouTubeProviderException youtube when youtube.StatusCode == System.Net.HttpStatusCode.Unauthorized =>
                Results.Json(new AppErrorDto("provider_unauthorized", youtube.Message, correlationId), statusCode: StatusCodes.Status401Unauthorized),
            YouTubeProviderException youtube =>
                Results.BadRequest(new AppErrorDto("provider_error", youtube.Message, correlationId)),
            BandcampProviderException bandcamp when bandcamp.StatusCode == System.Net.HttpStatusCode.NotFound =>
                Results.Json(new AppErrorDto("provider_resource_not_found", bandcamp.Message, correlationId), statusCode: StatusCodes.Status404NotFound),
            BandcampProviderException bandcamp =>
                Results.BadRequest(new AppErrorDto("provider_error", bandcamp.Message, correlationId)),
            OAuthAuthorizationException oauth =>
                Results.BadRequest(new AppErrorDto("oauth_error", oauth.Message, correlationId)),
            ArgumentException argument =>
                Results.BadRequest(new AppErrorDto("invalid_provider_request", argument.Message, correlationId)),
            InvalidOperationException invalid =>
                Results.BadRequest(new AppErrorDto("provider_operation_failed", invalid.Message, correlationId)),
            KeyNotFoundException missing =>
                Results.Json(new AppErrorDto("provider_resource_not_found", missing.Message, correlationId), statusCode: StatusCodes.Status404NotFound),
            _ => Results.BadRequest(new AppErrorDto("provider_error", exception.Message, correlationId)),
        };
    }

    private static async Task MarkProviderAuthorizationExpiredAsync(
        Exception exception,
        Guid accountId,
        ExternalAccountStore accounts,
        CancellationToken cancellationToken)
    {
        if (exception is YouTubeProviderException { ReauthorizationRequired: true })
            await accounts.MarkAuthorizationExpiredAsync(accountId, cancellationToken);
    }

    private static string GetOpenApiVersion()
    {
        var assembly = typeof(ServerHost).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";

        var metadataIndex = version.IndexOf('+', StringComparison.Ordinal);
        return metadataIndex >= 0 ? version[..metadataIndex] : version;
    }

    private static bool IsOpenApiGenerationProcess()
        => Environment.GetCommandLineArgs().Any(argument =>
            argument.Contains("dotnet-getdocument", StringComparison.OrdinalIgnoreCase)
            || argument.Contains("GetDocument", StringComparison.OrdinalIgnoreCase));

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/", () => Results.Redirect("/api/server/info"))
            .ExcludeFromDescription();

        app.MapGet("/health", (HttpContext context, EngineSupervisor supervisor) => Results.Ok(supervisor.GetSystemHealth(GetCorrelationId(context))))
            .WithTags("System")
            .WithSummary("Gets a minimal daemon health response.")
            .Produces<SystemHealthDto>();

        app.MapGet("/api/server/info", (EngineSupervisor supervisor) => Results.Ok(supervisor.GetInfo()))
            .WithTags("Server")
            .WithSummary("Gets server identity and protocol information.")
            .Produces<ServerInfoDto>();
        app.MapGet("/api/v1/system/info", (EngineSupervisor supervisor) => Results.Ok(supervisor.GetSystemInfo()))
            .WithTags("System")
            .WithSummary("Gets versioned application API system information.")
            .Produces<SystemInfoDto>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);
        app.MapGet("/api/v1/system/health", (HttpContext context, EngineSupervisor supervisor) => Results.Ok(supervisor.GetSystemHealth(GetCorrelationId(context))))
            .WithTags("System")
            .WithSummary("Gets application API health and correlation metadata.")
            .Produces<SystemHealthDto>()
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);
        app.MapGet("/api/v1/system/capabilities", (EngineSupervisor supervisor) => Results.Ok(supervisor.GetSystemCapabilities()))
            .WithTags("System")
            .WithSummary("Gets the versioned application API capability snapshot.")
            .Produces<SystemCapabilitiesDto>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/v1/providers", (ProviderCapabilityRegistry providers) =>
                Results.Ok(providers.List().Select(ToProviderCapabilityDto).ToArray()))
            .WithTags("Providers")
            .WithSummary("Lists external playlist and metadata provider capabilities.")
            .Produces<IReadOnlyList<ProviderCapabilityDto>>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/v1/providers/{providerId}/capabilities", (string providerId, ProviderCapabilityRegistry providers) =>
            {
                var provider = providers.Find(providerId);
                return provider is null
                    ? Results.NotFound()
                    : Results.Ok(ToProviderCapabilityDto(provider));
            })
            .WithTags("Providers")
            .WithSummary("Gets capability flags for a single external provider.")
            .Produces<ProviderCapabilityDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/providers/{providerId}/authorization/start", async (
            string providerId,
            ProviderAuthorizationStartRequestDto request,
            HttpContext context,
            OAuthPkceCoordinator oauth,
            IEnumerable<IPlaylistSourceProvider> providers,
            IOptions<ServerOptions> serverOptions) =>
        {
            if (!TryCreateUri(request.RedirectUri, out var redirectUri))
                return BadAppRequest(context, "invalid_redirect_uri", "A valid loopback redirect URI is required.");
            if (!IsProviderConfigured(providerId, serverOptions.Value))
                return BadAppRequest(context, "provider_not_configured", $"Provider '{providerId}' is not configured for account authorization.");

            var provider = FindPlaylistProvider(providers, providerId);
            if (provider == null)
                return BadAppRequest(context, "provider_not_configured", $"Provider '{providerId}' is not configured for account authorization.");

            var scopes = GetProviderDefaultScopes(providerId);
            var session = oauth.CreateSession(providerId, redirectUri, scopes);
            try
            {
                var start = await provider.StartAuthorizationAsync(session.ToAuthorizationRequest(), context.RequestAborted);
                return Results.Ok(new ProviderAuthorizationStartDto(
                    providerId,
                    start.AuthorizationUri.ToString(),
                    start.State,
                    session.ExpiresAtUtc));
            }
            catch (Exception ex) when (IsProviderFacingException(ex))
            {
                return ProviderError(context, ex);
            }
        })
            .WithTags("Providers")
            .WithSummary("Starts an external provider OAuth authorization flow without exposing PKCE verifier state.")
            .Produces<ProviderAuthorizationStartDto>()
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/providers/{providerId}/authorization/complete", async (
            string providerId,
            ProviderAuthorizationCallbackRequestDto request,
            HttpContext context,
            OAuthPkceCoordinator oauth,
            IEnumerable<IPlaylistSourceProvider> providers,
            IOptions<ServerOptions> serverOptions,
            ExternalAccountStore accounts,
            ServerDatabaseMigrationService databaseMigration,
            SockseekDbContext db,
            CancellationToken ct) =>
        {
            if (!TryCreateUri(request.RedirectUri, out var redirectUri))
                return BadAppRequest(context, "invalid_redirect_uri", "A valid loopback redirect URI is required.");
            if (!IsProviderConfigured(providerId, serverOptions.Value))
                return BadAppRequest(context, "provider_not_configured", $"Provider '{providerId}' is not configured for account authorization.");

            var provider = FindPlaylistProvider(providers, providerId);
            if (provider == null)
                return BadAppRequest(context, "provider_not_configured", $"Provider '{providerId}' is not configured for account authorization.");

            OAuthAuthorizationCompletion completion;
            try
            {
                completion = oauth.Complete(new AuthorizationCallback(
                    providerId,
                    redirectUri,
                    request.State,
                    request.Code,
                    request.Error));
            }
            catch (OAuthAuthorizationException ex)
            {
                return BadAppRequest(context, "oauth_state_invalid", ex.Message);
            }

            try
            {
                await databaseMigration.EnsureMigratedAsync(ct);
                var providerAccount = await provider.CompleteAuthorizationAsync(
                    completion.Callback with { CodeVerifier = completion.CodeVerifier },
                    ct);
                var externalProvider = ToExternalProvider(providerId);
                if (externalProvider == null)
                    return BadAppRequest(context, "provider_not_supported", $"Provider '{providerId}' cannot persist accounts.");

                var accountId = await accounts.UpsertAuthorizedAsync(externalProvider.Value, providerAccount, ct);
                var account = await db.ExternalAccounts.AsNoTracking().SingleAsync(entity => entity.Id == accountId, ct);
                return Results.Ok(ToExternalAccountDto(account));
            }
            catch (Exception ex) when (IsProviderFacingException(ex))
            {
                return ProviderError(context, ex);
            }
        })
            .WithTags("Providers")
            .WithSummary("Completes an external provider OAuth authorization flow and stores only an opaque secret reference.")
            .Produces<ExternalAccountDto>()
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/providers/{providerId}/public-playlists/import", async (
            string providerId,
            ImportProviderPublicUrlRequestDto request,
            HttpContext context,
            IEnumerable<IPlaylistSourceProvider> providers,
            ServerDatabaseMigrationService databaseMigration,
            ExternalPlaylistSnapshotStore snapshots,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return BadAppRequest(context, "invalid_public_url", "A public provider URL is required.");
            if (!Enum.TryParse<PlaylistImportMode>(request.ImportMode, ignoreCase: true, out var importMode))
                return BadAppRequest(context, "invalid_import_mode", "ImportMode must be Copy or Mirror.");

            var provider = FindPlaylistProvider(providers, providerId);
            if (provider == null || !provider.Capabilities.HasFlag(PlaylistProviderCapabilities.ImportPublicUrl))
                return BadAppRequest(context, "provider_not_configured", $"Provider '{providerId}' is not configured for public URL import.");

            try
            {
                await databaseMigration.EnsureMigratedAsync(ct);
                var playlist = await provider.GetPlaylistAsync(new ExternalPlaylistRequest(
                    null,
                    providerId,
                    request.Url,
                    request.Url), ct);
                var record = ExternalPlaylistSnapshotRecordFactory.FromProviderSnapshot(
                    playlist,
                    importMode,
                    playlistName: request.PlaylistName);
                var playlistId = await snapshots.UpsertAsync(record, ct);
                return Results.Ok(new ImportedPlaylistDto(
                    playlistId,
                    providerId,
                    playlist.ExternalPlaylistId,
                    playlist.Name,
                    importMode.ToString(),
                    playlist.Items.Count));
            }
            catch (Exception ex) when (IsProviderFacingException(ex))
            {
                return ProviderError(context, ex);
            }
        })
            .WithTags("Providers")
            .WithSummary("Imports a public provider playlist URL into the local playlist store.")
            .Produces<ImportedPlaylistDto>()
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/v1/accounts", async (
            ServerDatabaseMigrationService databaseMigration,
            SockseekDbContext db,
            CancellationToken ct) =>
            {
                await databaseMigration.EnsureMigratedAsync(ct);
                var accounts = await db.ExternalAccounts
                    .AsNoTracking()
                    .OrderBy(account => account.Provider)
                    .ThenBy(account => account.DisplayName)
                    .ThenBy(account => account.ExternalUserId)
                    .ToListAsync(ct);

                return Results.Ok(accounts.Select(ToExternalAccountDto).ToArray());
            })
            .WithTags("Accounts")
            .WithSummary("Lists connected external provider accounts without exposing secret references.")
            .Produces<IReadOnlyList<ExternalAccountDto>>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/accounts/{accountId:guid}/disconnect", async (
            Guid accountId,
            ExternalAccountStore accounts,
            ServerDatabaseMigrationService databaseMigration,
            SockseekDbContext db,
            ISecretStore secretStore,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            var disconnected = await accounts.DisconnectAsync(accountId, secretStore, ct);
            if (!disconnected)
                return Results.NotFound();

            var account = await db.ExternalAccounts
                .AsNoTracking()
                .SingleAsync(entity => entity.Id == accountId, ct);
            return Results.Ok(ToExternalAccountDto(account));
        })
            .WithTags("Accounts")
            .WithSummary("Disconnects an external account, deletes its stored secret and preserves local playlist data.")
            .Produces<ExternalAccountDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/v1/accounts/{accountId:guid}/provider-playlists", async (
            Guid accountId,
            HttpContext context,
            IEnumerable<IPlaylistSourceProvider> providers,
            IOptions<ServerOptions> serverOptions,
            ServerDatabaseMigrationService databaseMigration,
            SockseekDbContext db,
            ExternalAccountStore accounts,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            var account = await db.ExternalAccounts.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == accountId, ct);
            if (account == null)
                return Results.NotFound();
            if (account.Status != (int)ExternalAccountStatus.Authorized)
                return BadAppRequest(context, "account_not_authorized", "The external account is not authorized.");

            var providerId = ToProviderId(account.Provider);
            if (!IsProviderConfigured(providerId, serverOptions.Value))
                return BadAppRequest(context, "provider_not_configured", $"Provider '{providerId}' is not configured for playlist import.");
            var provider = FindPlaylistProvider(providers, providerId);
            if (provider == null)
                return BadAppRequest(context, "provider_not_configured", $"Provider '{providerId}' is not configured for playlist import.");

            RememberProviderAccount(provider, account);
            try
            {
                var playlists = await provider.GetPlaylistsAsync(new ExternalAccountId(account.Id), ct);
                return Results.Ok(playlists.Select(ToExternalPlaylistSummaryDto).ToArray());
            }
            catch (Exception ex) when (IsProviderFacingException(ex))
            {
                await MarkProviderAuthorizationExpiredAsync(ex, account.Id, accounts, ct);
                return ProviderError(context, ex);
            }
        })
            .WithTags("Accounts")
            .WithSummary("Lists playlists visible to a connected external provider account.")
            .Produces<IReadOnlyList<ExternalPlaylistSummaryDto>>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/accounts/{accountId:guid}/provider-playlists/{externalPlaylistId}/import", async (
            Guid accountId,
            string externalPlaylistId,
            ImportProviderPlaylistRequestDto request,
            HttpContext context,
            IEnumerable<IPlaylistSourceProvider> providers,
            IOptions<ServerOptions> serverOptions,
            ServerDatabaseMigrationService databaseMigration,
            SockseekDbContext db,
            ExternalPlaylistSnapshotStore snapshots,
            ExternalAccountStore accounts,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            var account = await db.ExternalAccounts.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == accountId, ct);
            if (account == null)
                return Results.NotFound();
            if (account.Status != (int)ExternalAccountStatus.Authorized)
                return BadAppRequest(context, "account_not_authorized", "The external account is not authorized.");
            if (!Enum.TryParse<PlaylistImportMode>(request.ImportMode, ignoreCase: true, out var importMode))
                return BadAppRequest(context, "invalid_import_mode", "ImportMode must be Copy or Mirror.");

            var providerId = ToProviderId(account.Provider);
            if (!IsProviderConfigured(providerId, serverOptions.Value))
                return BadAppRequest(context, "provider_not_configured", $"Provider '{providerId}' is not configured for playlist import.");
            var provider = FindPlaylistProvider(providers, providerId);
            if (provider == null)
                return BadAppRequest(context, "provider_not_configured", $"Provider '{providerId}' is not configured for playlist import.");

            RememberProviderAccount(provider, account);
            try
            {
                var playlist = await provider.GetPlaylistAsync(new ExternalPlaylistRequest(
                    new ExternalAccountId(account.Id),
                    providerId,
                    externalPlaylistId,
                    null), ct);
                var accountSnapshot = ToProviderAccountSnapshot(account);
                var record = ExternalPlaylistSnapshotRecordFactory.FromProviderSnapshot(
                    playlist,
                    importMode,
                    accountSnapshot,
                    request.PlaylistName);
                var playlistId = await snapshots.UpsertAsync(record, ct);
                return Results.Ok(new ImportedPlaylistDto(
                    playlistId,
                    providerId,
                    playlist.ExternalPlaylistId,
                    playlist.Name,
                    importMode.ToString(),
                    playlist.Items.Count));
            }
            catch (Exception ex) when (IsProviderFacingException(ex))
            {
                await MarkProviderAuthorizationExpiredAsync(ex, account.Id, accounts, ct);
                return ProviderError(context, ex);
            }
        })
            .WithTags("Accounts")
            .WithSummary("Imports or syncs a provider playlist into the local playlist store.")
            .Produces<ImportedPlaylistDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/v1/playlists", async (
            ServerDatabaseMigrationService databaseMigration,
            PlaylistWorkflowSyncService workflowSync,
            PlaylistQueryStore playlists,
            CancellationToken ct) =>
            {
                await databaseMigration.EnsureMigratedAsync(ct);
                await workflowSync.SyncAllAsync(ct);
                var summaries = await playlists.GetSummariesAsync(ct);
                return Results.Ok(summaries.Select(ToPlaylistSummaryDto).ToArray());
            })
            .WithTags("Playlists")
            .WithSummary("Lists local playlists with resolution summary counts.")
            .Produces<IReadOnlyList<PlaylistSummaryDto>>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/v1/playlists/{playlistId:guid}", async (
            Guid playlistId,
            ServerDatabaseMigrationService databaseMigration,
            PlaylistWorkflowSyncService workflowSync,
            PlaylistQueryStore playlists,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            await workflowSync.SyncPlaylistAsync(playlistId, ct);
            var playlist = await playlists.GetDetailAsync(playlistId, ct);
            return playlist == null
                ? Results.NotFound()
                : Results.Ok(ToPlaylistDetailDto(playlist));
        })
            .WithTags("Playlists")
            .WithSummary("Gets a local playlist and its imported item metadata.")
            .Produces<PlaylistDetailDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/playlists/{playlistId:guid}/resolve-local", async (
            Guid playlistId,
            ServerDatabaseMigrationService databaseMigration,
            PlaylistQueryStore playlists,
            PlaylistLocalMatchResolver resolver,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            var existing = await playlists.GetDetailAsync(playlistId, ct);
            if (existing == null)
                return Results.NotFound();

            var result = await resolver.ResolveAsync(playlistId, ct);
            var updated = await playlists.GetDetailAsync(playlistId, ct)
                ?? throw new InvalidOperationException("Playlist disappeared during local resolution.");
            var detail = ToPlaylistDetailDto(updated);
            return Results.Ok(new PlaylistLocalResolveResultDto(
                result.MatchedItems,
                result.ReviewItems,
                result.UnresolvedItems,
                detail.Resolution,
                detail));
        })
            .WithTags("Playlists")
            .WithSummary("Resolves imported playlist items against locally available library files.")
            .Produces<PlaylistLocalResolveResultDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/playlists/{playlistId:guid}/download-missing", async (
            Guid playlistId,
            ServerDatabaseMigrationService databaseMigration,
            PlaylistDownloadOrchestrator downloads,
            PlaylistQueryStore playlists,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            var result = await downloads.DownloadMissingAsync(playlistId, ct);
            if (!result.PlaylistFound)
                return Results.NotFound();

            var updated = await playlists.GetDetailAsync(playlistId, ct)
                ?? throw new InvalidOperationException("Playlist disappeared during missing-item download submission.");
            var detail = ToPlaylistDetailDto(updated);
            return Results.Ok(new PlaylistDownloadMissingResultDto(
                result.SubmittedItems,
                result.FailedItems,
                result.SkippedItems,
                detail.Resolution,
                detail,
                result.Submissions.Select(ToPlaylistDownloadSubmissionDto).ToArray()));
        })
            .WithTags("Playlists")
            .WithSummary("Submits missing imported playlist items to Soulseek download workflows.")
            .Produces<PlaylistDownloadMissingResultDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/playlists/{playlistId:guid}/cancel-active-downloads", async (
            Guid playlistId,
            ServerDatabaseMigrationService databaseMigration,
            PlaylistDownloadOrchestrator downloads,
            PlaylistWorkflowSyncService workflowSync,
            PlaylistQueryStore playlists,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            var result = await downloads.CancelActiveDownloadsAsync(playlistId, ct);
            if (!result.PlaylistFound)
                return Results.NotFound();

            await workflowSync.SyncPlaylistAsync(playlistId, ct);
            var updated = await playlists.GetDetailAsync(playlistId, ct)
                ?? throw new InvalidOperationException("Playlist disappeared during active download cancellation.");
            var detail = ToPlaylistDetailDto(updated);
            return Results.Ok(new PlaylistCancelDownloadsResultDto(
                result.CancelledItems,
                result.FailedItems,
                detail.Resolution,
                detail));
        })
            .WithTags("Playlists")
            .WithSummary("Cancels active Soulseek workflows linked to a playlist without touching completed local files.")
            .Produces<PlaylistCancelDownloadsResultDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/playlists/{playlistId:guid}/play-available", async (
            Guid playlistId,
            HttpContext context,
            ServerDatabaseMigrationService databaseMigration,
            PlaylistWorkflowSyncService workflowSync,
            SockseekDbContext db,
            PlaybackCoordinator player,
            LocalArtworkCache artworkCache,
            PlaybackQueuePersistenceService queuePersistence,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            await workflowSync.SyncPlaylistAsync(playlistId, ct);
            var playlistExists = await db.Playlists
                .AsNoTracking()
                .AnyAsync(playlist => playlist.Id == playlistId, ct);
            if (!playlistExists)
                return Results.NotFound();

            var queueItems = await GetAvailablePlaylistQueueItemsAsync(db, playlistId, startAfterPosition: null, ct);
            if (queueItems.Count == 0)
                return BadAppRequest(context, "playlist_no_available_items", "The playlist does not have any available local items to play.");

            player.SetQueue(queueItems);
            var snapshot = await player.PlayCurrentAsync(ct);
            return Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, snapshot, ct));
        })
            .WithTags("Playlists")
            .WithSummary("Starts local playback for the available items in a playlist.")
            .Produces<PlayerStateDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/playlists/{playlistId:guid}/items/{playlistItemId:guid}/play-from-here", async (
            Guid playlistId,
            Guid playlistItemId,
            HttpContext context,
            ServerDatabaseMigrationService databaseMigration,
            PlaylistWorkflowSyncService workflowSync,
            SockseekDbContext db,
            PlaybackCoordinator player,
            LocalArtworkCache artworkCache,
            PlaybackQueuePersistenceService queuePersistence,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            await workflowSync.SyncPlaylistAsync(playlistId, ct);
            var startItem = await db.PlaylistItems
                .AsNoTracking()
                .Where(item => item.Id == playlistItemId && item.PlaylistId == playlistId)
                .Select(item => new { item.Position, item.RemovedAtUtc })
                .SingleOrDefaultAsync(ct);
            if (startItem == null)
                return Results.NotFound();
            if (startItem.RemovedAtUtc.HasValue)
                return BadAppRequest(context, "playlist_item_removed", "Removed provider playlist items cannot start playback.");

            var queueItems = await GetAvailablePlaylistQueueItemsAsync(db, playlistId, startItem.Position, ct);
            if (queueItems.Count == 0)
                return BadAppRequest(context, "playlist_no_available_items", "No available local playlist items exist at or after the selected item.");

            player.SetQueue(queueItems);
            var snapshot = await player.PlayCurrentAsync(ct);
            return Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, snapshot, ct));
        })
            .WithTags("Playlists")
            .WithSummary("Starts local playback from the selected playlist position, skipping unavailable items.")
            .Produces<PlayerStateDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/playlists/{playlistId:guid}/items/{playlistItemId:guid}/skip", async (
            Guid playlistId,
            Guid playlistItemId,
            HttpContext context,
            ServerDatabaseMigrationService databaseMigration,
            SockseekDbContext db,
            PlaylistQueryStore playlists,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            var item = await db.PlaylistItems
                .SingleOrDefaultAsync(entity => entity.Id == playlistItemId && entity.PlaylistId == playlistId, ct);
            if (item == null)
                return Results.NotFound();
            if (item.RemovedAtUtc.HasValue)
                return BadAppRequest(context, "playlist_item_removed", "Removed provider playlist items cannot be skipped.");

            item.Status = (int)PlaylistItemStatus.Skipped;
            await db.SaveChangesAsync(ct);
            var updated = await playlists.GetDetailAsync(playlistId, ct)
                ?? throw new InvalidOperationException("Playlist disappeared during item skip.");
            return Results.Ok(ToPlaylistDetailDto(updated));
        })
            .WithTags("Playlists")
            .WithSummary("Marks a local playlist item as skipped without deleting provider data or local files.")
            .Produces<PlaylistDetailDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/playlists/{playlistId:guid}/items/{playlistItemId:guid}/approve-local", async (
            Guid playlistId,
            Guid playlistItemId,
            HttpContext context,
            ServerDatabaseMigrationService databaseMigration,
            SockseekDbContext db,
            PlaylistQueryStore playlists,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            var item = await db.PlaylistItems
                .SingleOrDefaultAsync(entity => entity.Id == playlistItemId && entity.PlaylistId == playlistId, ct);
            if (item == null)
                return Results.NotFound();
            if (item.RemovedAtUtc.HasValue)
                return BadAppRequest(context, "playlist_item_removed", "Removed provider playlist items cannot be approved.");
            if (item.Status != (int)PlaylistItemStatus.ReviewRequired || !item.CanonicalTrackId.HasValue)
                return BadAppRequest(context, "playlist_item_not_reviewable", "Only local review-required playlist items can be approved.");

            db.ResolutionAttempts.Add(new ResolutionAttemptEntity
            {
                Id = Guid.NewGuid(),
                PlaylistItemId = item.Id,
                CandidateTrackId = item.CanonicalTrackId,
                Method = (int)ResolutionMethod.ManualReview,
                Score = 1d,
                Decision = (int)ResolutionDecision.UserApproved,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            item.Status = (int)PlaylistItemStatus.AvailableLocal;
            await db.SaveChangesAsync(ct);
            var updated = await playlists.GetDetailAsync(playlistId, ct)
                ?? throw new InvalidOperationException("Playlist disappeared during local match approval.");
            return Results.Ok(ToPlaylistDetailDto(updated));
        })
            .WithTags("Playlists")
            .WithSummary("Approves a review-required local playlist item match.")
            .Produces<PlaylistDetailDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/playlists/{playlistId:guid}/items/{playlistItemId:guid}/reject-local", async (
            Guid playlistId,
            Guid playlistItemId,
            HttpContext context,
            ServerDatabaseMigrationService databaseMigration,
            SockseekDbContext db,
            PlaylistQueryStore playlists,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            var item = await db.PlaylistItems
                .SingleOrDefaultAsync(entity => entity.Id == playlistItemId && entity.PlaylistId == playlistId, ct);
            if (item == null)
                return Results.NotFound();
            if (item.RemovedAtUtc.HasValue)
                return BadAppRequest(context, "playlist_item_removed", "Removed provider playlist items cannot be rejected.");
            if (item.Status != (int)PlaylistItemStatus.ReviewRequired || !item.CanonicalTrackId.HasValue)
                return BadAppRequest(context, "playlist_item_not_reviewable", "Only local review-required playlist items can be rejected.");

            var rejectedTrackId = item.CanonicalTrackId.Value;
            db.ResolutionAttempts.Add(new ResolutionAttemptEntity
            {
                Id = Guid.NewGuid(),
                PlaylistItemId = item.Id,
                CandidateTrackId = rejectedTrackId,
                Method = (int)ResolutionMethod.ManualReview,
                Score = 0d,
                Decision = (int)ResolutionDecision.UserRejected,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            item.CanonicalTrackId = null;
            item.Status = (int)PlaylistItemStatus.Unresolved;
            await db.SaveChangesAsync(ct);
            var updated = await playlists.GetDetailAsync(playlistId, ct)
                ?? throw new InvalidOperationException("Playlist disappeared during local match rejection.");
            return Results.Ok(ToPlaylistDetailDto(updated));
        })
            .WithTags("Playlists")
            .WithSummary("Rejects a review-required local playlist item match and keeps the decision for future resolves.")
            .Produces<PlaylistDetailDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/playlists/{playlistId:guid}/items/{playlistItemId:guid}/map-local", async (
            Guid playlistId,
            Guid playlistItemId,
            MapPlaylistItemLocalRequestDto request,
            HttpContext context,
            ServerDatabaseMigrationService databaseMigration,
            SockseekDbContext db,
            PlaylistQueryStore playlists,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            var item = await db.PlaylistItems
                .SingleOrDefaultAsync(entity => entity.Id == playlistItemId && entity.PlaylistId == playlistId, ct);
            if (item == null)
                return Results.NotFound();
            if (item.RemovedAtUtc.HasValue)
                return BadAppRequest(context, "playlist_item_removed", "Removed provider playlist items cannot be manually mapped.");

            var status = Enum.IsDefined(typeof(PlaylistItemStatus), item.Status)
                ? (PlaylistItemStatus)item.Status
                : PlaylistItemStatus.Unresolved;
            if (status is PlaylistItemStatus.Searching
                or PlaylistItemStatus.CandidateFound
                or PlaylistItemStatus.Downloading)
            {
                return BadAppRequest(context, "playlist_item_active_workflow", "Active playlist resolution workflows must finish or be cancelled before manual mapping.");
            }

            var hasAvailableLocalFile = await db.CanonicalTracks
                .AsNoTracking()
                .AnyAsync(track => track.Id == request.CanonicalTrackId
                    && track.LocalMediaFiles.Any(file => file.Availability == (int)LocalMediaAvailability.Available), ct);
            if (!hasAvailableLocalFile)
                return BadAppRequest(context, "local_track_unavailable", "Manual playlist mapping requires a local library track with an available media file.");

            item.CanonicalTrackId = request.CanonicalTrackId;
            item.Status = (int)PlaylistItemStatus.AvailableLocal;
            db.ResolutionAttempts.Add(new ResolutionAttemptEntity
            {
                Id = Guid.NewGuid(),
                PlaylistItemId = item.Id,
                CandidateTrackId = request.CanonicalTrackId,
                Method = (int)ResolutionMethod.ManualReview,
                Score = 1d,
                Decision = (int)ResolutionDecision.UserApproved,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);

            var updated = await playlists.GetDetailAsync(playlistId, ct)
                ?? throw new InvalidOperationException("Playlist disappeared during manual local mapping.");
            return Results.Ok(ToPlaylistDetailDto(updated));
        })
            .WithTags("Playlists")
            .WithSummary("Manually maps a playlist item to an available local library track.")
            .Produces<PlaylistDetailDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/playlists/{playlistId:guid}/items/{playlistItemId:guid}/retry", async (
            Guid playlistId,
            Guid playlistItemId,
            HttpContext context,
            ServerDatabaseMigrationService databaseMigration,
            PlaylistDownloadOrchestrator downloads,
            PlaylistQueryStore playlists,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            var result = await downloads.RetryItemAsync(playlistId, playlistItemId, ct);
            if (!result.ItemFound)
                return Results.NotFound();

            switch (result.Outcome)
            {
                case PlaylistItemRetryOutcome.Removed:
                    return BadAppRequest(context, "playlist_item_removed", "Removed provider playlist items cannot be retried.");
                case PlaylistItemRetryOutcome.AlreadyAvailable:
                    return BadAppRequest(context, "playlist_item_available", "Available local playlist items do not need retry.");
                case PlaylistItemRetryOutcome.NotRetryable:
                    return BadAppRequest(context, "playlist_item_not_retryable", "Only failed or skipped playlist items can be retried.");
            }

            var updated = await playlists.GetDetailAsync(playlistId, ct)
                ?? throw new InvalidOperationException("Playlist disappeared during item retry.");
            var detail = ToPlaylistDetailDto(updated);
            PlaylistDownloadSubmissionDto[] submissions = result.Submission is { } submission
                ? new[] { ToPlaylistDownloadSubmissionDto(submission) }
                : Array.Empty<PlaylistDownloadSubmissionDto>();
            return Results.Ok(new PlaylistDownloadMissingResultDto(
                result.Outcome == PlaylistItemRetryOutcome.Submitted ? 1 : 0,
                result.Outcome == PlaylistItemRetryOutcome.FailedToSubmit ? 1 : 0,
                0,
                detail.Resolution,
                detail,
                submissions));
        })
            .WithTags("Playlists")
            .WithSummary("Retries a failed or skipped playlist item by submitting a new Soulseek download workflow.")
            .Produces<PlaylistDownloadMissingResultDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/playlists/{playlistId:guid}/items/{playlistItemId:guid}/download", async (
            Guid playlistId,
            Guid playlistItemId,
            HttpContext context,
            ServerDatabaseMigrationService databaseMigration,
            PlaylistDownloadOrchestrator downloads,
            PlaylistQueryStore playlists,
            CancellationToken ct) =>
        {
            await databaseMigration.EnsureMigratedAsync(ct);
            var result = await downloads.DownloadItemAsync(playlistId, playlistItemId, ct);
            if (!result.ItemFound)
                return Results.NotFound();

            switch (result.Outcome)
            {
                case PlaylistItemDownloadOutcome.Removed:
                    return BadAppRequest(context, "playlist_item_removed", "Removed provider playlist items cannot be downloaded.");
                case PlaylistItemDownloadOutcome.AlreadyAvailable:
                    return BadAppRequest(context, "playlist_item_available", "Available local playlist items do not need download.");
                case PlaylistItemDownloadOutcome.AlreadyActive:
                    return BadAppRequest(context, "playlist_item_already_active", "Playlist item already has an active resolution workflow.");
                case PlaylistItemDownloadOutcome.NotDownloadable:
                    return BadAppRequest(context, "playlist_item_not_downloadable", "Only unresolved, failed or skipped playlist items can be downloaded.");
            }

            var updated = await playlists.GetDetailAsync(playlistId, ct)
                ?? throw new InvalidOperationException("Playlist disappeared during item download.");
            var detail = ToPlaylistDetailDto(updated);
            PlaylistDownloadSubmissionDto[] submissions = result.Submission is { } submission
                ? new[] { ToPlaylistDownloadSubmissionDto(submission) }
                : Array.Empty<PlaylistDownloadSubmissionDto>();
            return Results.Ok(new PlaylistDownloadMissingResultDto(
                result.Outcome == PlaylistItemDownloadOutcome.Submitted ? 1 : 0,
                result.Outcome == PlaylistItemDownloadOutcome.FailedToSubmit ? 1 : 0,
                0,
                detail.Resolution,
                detail,
                submissions));
        })
            .WithTags("Playlists")
            .WithSummary("Submits one unresolved playlist item to a Soulseek download workflow.")
            .Produces<PlaylistDownloadMissingResultDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/v1/player", async (PlaybackCoordinator player, SockseekDbContext db, LocalArtworkCache artworkCache, CancellationToken ct) =>
                Results.Ok(await ToPlayerStateDtoAsync(player, db, artworkCache, ct)))
            .WithTags("Player")
            .WithSummary("Gets the current local player state.")
            .Produces<PlayerStateDto>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/player/play/canonical-track", async (
            PlayCanonicalTrackRequestDto request,
            PlaybackCoordinator player,
            SockseekDbContext db,
            LocalArtworkCache artworkCache,
            PlaybackQueuePersistenceService queuePersistence,
            CancellationToken ct) =>
                Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, await player.PlayCanonicalTrackAsync(request.CanonicalTrackId, ct), ct)))
            .WithTags("Player")
            .WithSummary("Starts playback for a canonical track resolved to a local file.")
            .Produces<PlayerStateDto>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/player/play/playlist-item", async (
            PlayPlaylistItemRequestDto request,
            PlaybackCoordinator player,
            SockseekDbContext db,
            LocalArtworkCache artworkCache,
            PlaybackQueuePersistenceService queuePersistence,
            CancellationToken ct) =>
                Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, await player.PlayPlaylistItemAsync(request.PlaylistItemId, ct), ct)))
            .WithTags("Player")
            .WithSummary("Starts playback for a playlist item resolved to a local file.")
            .Produces<PlayerStateDto>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/player/play/download-job", async (
            PlayDownloadJobRequestDto request,
            EngineStateStore stateStore,
            IOptions<ServerOptions> serverOptions,
            PlaybackCoordinator player,
            SockseekDbContext db,
            LocalArtworkCache artworkCache,
            PlaybackQueuePersistenceService queuePersistence,
            CancellationToken ct) =>
        {
            var download = stateStore.GetActiveProgressiveDownloadSnapshot(
                request.JobId,
                extension => IsProgressivePlaybackEnabledForCodec(serverOptions.Value, extension));
            if (download == null)
                return Results.NotFound();

            var snapshot = await player.PlayProgressiveAsync(download.Source, download.Buffer, cancellationToken: ct);
            return Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, snapshot, ct));
        })
            .WithTags("Player")
            .WithSummary("Starts playback for an active Soulseek download when buffer policy permits it.")
            .Produces<PlayerStateDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/player/pause", async (PlaybackCoordinator player, SockseekDbContext db, LocalArtworkCache artworkCache, PlaybackQueuePersistenceService queuePersistence, CancellationToken ct) =>
                Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, await player.PauseAsync(ct), ct)))
            .WithTags("Player")
            .WithSummary("Pauses local playback when currently playing.")
            .Produces<PlayerStateDto>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/player/resume", async (PlaybackCoordinator player, SockseekDbContext db, LocalArtworkCache artworkCache, PlaybackQueuePersistenceService queuePersistence, CancellationToken ct) =>
                Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, await player.ResumeAsync(ct), ct)))
            .WithTags("Player")
            .WithSummary("Resumes local playback when currently paused.")
            .Produces<PlayerStateDto>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/player/stop", async (PlaybackCoordinator player, SockseekDbContext db, LocalArtworkCache artworkCache, PlaybackQueuePersistenceService queuePersistence, CancellationToken ct) =>
                Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, await player.StopAsync(ct), ct)))
            .WithTags("Player")
            .WithSummary("Stops local playback.")
            .Produces<PlayerStateDto>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/player/next", async (PlaybackCoordinator player, SockseekDbContext db, LocalArtworkCache artworkCache, PlaybackQueuePersistenceService queuePersistence, CancellationToken ct) =>
                Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, await player.NextAsync(ct), ct)))
            .WithTags("Player")
            .WithSummary("Moves to the next local queue item when available.")
            .Produces<PlayerStateDto>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/player/previous", async (PlaybackCoordinator player, SockseekDbContext db, LocalArtworkCache artworkCache, PlaybackQueuePersistenceService queuePersistence, CancellationToken ct) =>
                Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, await player.PreviousAsync(ct), ct)))
            .WithTags("Player")
            .WithSummary("Moves to the previous local queue item when available.")
            .Produces<PlayerStateDto>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/player/seek", async (
            SeekPlaybackRequestDto request,
            PlaybackCoordinator player,
            SockseekDbContext db,
            LocalArtworkCache artworkCache,
            PlaybackQueuePersistenceService queuePersistence,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, await player.SeekAsync(TimeSpan.FromMilliseconds(request.PositionMs), ct), ct));
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Player")
            .WithSummary("Seeks within the current local playback item.")
            .Produces<PlayerStateDto>()
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/player/volume", async (
            SetPlayerVolumeRequestDto request,
            PlaybackCoordinator player,
            SockseekDbContext db,
            LocalArtworkCache artworkCache,
            PlaybackQueuePersistenceService queuePersistence,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, await player.SetVolumeAsync(request.Volume, ct), ct));
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Player")
            .WithSummary("Sets local player volume from 0.0 to 1.0.")
            .Produces<PlayerStateDto>()
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/player/mute", async (
            SetPlayerMutedRequestDto request,
            PlaybackCoordinator player,
            SockseekDbContext db,
            LocalArtworkCache artworkCache,
            PlaybackQueuePersistenceService queuePersistence,
            CancellationToken ct) =>
                Results.Ok(await ToSavedPlayerStateDtoAsync(player, db, artworkCache, queuePersistence, await player.SetMutedAsync(request.IsMuted, ct), ct)))
            .WithTags("Player")
            .WithSummary("Sets local player mute state.")
            .Produces<PlayerStateDto>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/v1/library/roots", async (LocalLibraryEndpointService library, CancellationToken ct) =>
            Results.Ok(await library.ListRootsAsync(ct)))
            .WithTags("Library")
            .WithSummary("Lists configured local library roots.")
            .Produces<IReadOnlyList<LibraryRootDto>>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/library/roots", async (SaveLibraryRootRequestDto request, LocalLibraryEndpointService library, CancellationToken ct) =>
        {
            try
            {
                var root = await library.SaveRootAsync(request, ct);
                return Results.Ok(root);
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Library")
            .WithSummary("Adds or updates a local library root.")
            .Produces<LibraryRootDto>()
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapDelete("/api/v1/library/roots/{rootId:guid}", async (Guid rootId, LocalLibraryEndpointService library, CancellationToken ct) =>
            await library.DeleteRootAsync(rootId, ct) ? Results.NoContent() : Results.NotFound())
            .WithTags("Library")
            .WithSummary("Deletes a local library root record without deleting physical audio files.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/library/scan", async (LocalLibraryEndpointService library, CancellationToken ct) =>
            Results.Ok(await library.ScanAsync(ct)))
            .WithTags("Library")
            .WithSummary("Scans all enabled local library roots.")
            .Produces<LocalLibraryConfiguredScanResultDto>()
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/v1/library/tracks", async (
            string? searchText,
            int? offset,
            int? limit,
            bool? includeMissing,
            LocalLibraryEndpointService library,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await library.SearchTracksAsync(searchText, offset ?? 0, limit ?? 100, includeMissing ?? true, ct));
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Library")
            .WithSummary("Searches indexed local library tracks.")
            .Produces<LocalLibrarySearchResponseDto>()
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/v1/library/duplicates", async (
            int? limit,
            bool? includeMissing,
            LocalLibraryEndpointService library,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await library.GetDuplicateGroupsAsync(limit ?? 100, includeMissing ?? false, ct));
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Library")
            .WithSummary("Lists local tracks with duplicate local media files.")
            .Produces<IReadOnlyList<LocalLibraryDuplicateGroupDto>>()
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/v1/library/files/{localMediaFileId:guid}/relink", async (
            Guid localMediaFileId,
            RelinkLocalMediaFileRequestDto request,
            LocalLibraryEndpointService library,
            CancellationToken ct) =>
        {
            try
            {
                var result = await library.RelinkAsync(localMediaFileId, request, ct);
                return result != null ? Results.Ok(result) : Results.NotFound();
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Library")
            .WithSummary("Relinks an indexed local media file record to a new physical file path.")
            .Produces<LocalMediaFileRelinkResultDto>()
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<AppErrorDto>(StatusCodes.Status401Unauthorized)
            .Produces<AppErrorDto>(StatusCodes.Status500InternalServerError);
        app.MapGet("/api/server/status", (EngineSupervisor supervisor) => Results.Ok(supervisor.GetStatus()))
            .WithTags("Server")
            .WithSummary("Gets current daemon and Soulseek client status.")
            .Produces<ServerStatusDto>();
        app.MapGet("/api/profiles", (EngineSupervisor supervisor) => Results.Ok(supervisor.GetProfiles()))
            .WithTags("Profiles")
            .WithSummary("Lists configured download profiles.")
            .Produces<IReadOnlyList<ProfileSummaryDto>>();
        app.MapGet("/api/events/catalog", () => Results.Ok(ServerEventCatalog.All))
            .WithTags("Events")
            .WithSummary("Lists SignalR event types and their snapshot invalidation behavior.")
            .Produces<IReadOnlyList<ServerEventDescriptorDto>>();

        app.MapGet("/api/jobs", (
            EngineStateStore stateStore,
            ServerJobLifecycleState? lifecycleState,
            ServerJobTerminalOutcome? terminalOutcome,
            ServerJobSkipReason? skipReason,
            ServerJobKind? kind,
            Guid? workflowId,
            bool includeAll = false) =>
        {
            var jobs = stateStore.GetJobs(new JobQuery(lifecycleState, terminalOutcome, kind, workflowId, includeAll, skipReason));
            return Results.Ok(jobs);
        })
            .WithTags("Jobs")
            .WithSummary("Lists known jobs.")
            .WithDescription("Default results contain only execution roots where ParentJobId is null. Set includeAll=true for a flat list of every matching job.")
            .Produces<IReadOnlyList<JobSummaryDto>>();

        app.MapGet("/api/jobs/{jobId:guid}", (Guid jobId, EngineStateStore stateStore) =>
        {
            var detail = stateStore.GetJobDetail(jobId);
            return detail != null ? Results.Ok(detail) : Results.NotFound();
        })
            .WithTags("Jobs")
            .WithSummary("Gets a job snapshot by id.")
            .Produces<JobDetailDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/workflows/{workflowId:guid}/jobs/display/{displayId:int}", (Guid workflowId, int displayId, EngineSupervisor supervisor) =>
        {
            var detail = supervisor.GetJobDetailByDisplayId(workflowId, displayId);
            return detail != null ? Results.Ok(detail) : Results.NotFound();
        })
            .WithTags("Jobs")
            .WithSummary("Gets a job snapshot by workflow and display id.")
            .Produces<JobDetailDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/jobs/{jobId:guid}/raw", (Guid jobId, long afterSequence, EngineSupervisor supervisor) =>
        {
            var results = supervisor.GetSearchRawResults(jobId, afterSequence);
            return results != null ? Results.Ok(results) : Results.NotFound();
        })
            .WithTags("Search Results")
            .WithSummary("Gets raw search responses for a search job.")
            .WithDescription("Use afterSequence to incrementally fetch raw responses after the last seen sequence.")
            .Produces<IReadOnlyList<SearchRawResultDto>>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/jobs/{jobId:guid}/results/files", (Guid jobId, EngineSupervisor supervisor) =>
        {
            var results = supervisor.GetFileResults(jobId);
            return results != null ? Results.Ok(results) : Results.NotFound();
        })
            .WithTags("Search Results")
            .WithSummary("Gets file candidates for a search-like job.")
            .Produces<SearchResultSnapshotDto<FileCandidateDto>>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/jobs/{jobId:guid}/results/files/project", (Guid jobId, FileSearchProjectionRequestDto request, EngineSupervisor supervisor) =>
        {
            var results = supervisor.GetFileResults(jobId, request);
            return results != null ? Results.Ok(results) : Results.NotFound();
        })
            .WithTags("Search Results")
            .WithSummary("Projects search results as file candidates.")
            .Produces<SearchResultSnapshotDto<FileCandidateDto>>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/jobs/{jobId:guid}/results/folders", (Guid jobId, bool includeFiles, EngineSupervisor supervisor) =>
        {
            try
            {
                var results = supervisor.GetFolderResults(jobId, includeFiles);
                return results != null ? Results.Ok(results) : Results.NotFound();
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Search Results")
            .WithSummary("Gets folder candidates for an album search-like job.")
            .WithDescription("Set includeFiles=true only when the client needs selectable files. Folder file counts can come from search results and may not represent a full browse of the remote folder.")
            .Produces<SearchResultSnapshotDto<AlbumFolderDto>>()
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/jobs/{jobId:guid}/results/folders/project", (Guid jobId, FolderSearchProjectionRequestDto request, EngineSupervisor supervisor) =>
        {
            try
            {
                var results = supervisor.GetFolderResults(jobId, request);
                return results != null ? Results.Ok(results) : Results.NotFound();
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Search Results")
            .WithSummary("Projects search results as album folders.")
            .Produces<SearchResultSnapshotDto<AlbumFolderDto>>()
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/jobs/{jobId:guid}/results/aggregate-tracks", (Guid jobId, EngineSupervisor supervisor) =>
        {
            var results = supervisor.GetAggregateTrackResults(jobId);
            return results != null ? Results.Ok(results) : Results.NotFound();
        })
            .WithTags("Search Results")
            .WithSummary("Gets aggregate track candidates.")
            .Produces<SearchResultSnapshotDto<AggregateTrackCandidateDto>>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/jobs/{jobId:guid}/results/aggregate-tracks/project", (Guid jobId, AggregateTrackProjectionRequestDto request, EngineSupervisor supervisor) =>
        {
            var results = supervisor.GetAggregateTrackResults(jobId, request);
            return results != null ? Results.Ok(results) : Results.NotFound();
        })
            .WithTags("Search Results")
            .WithSummary("Projects search results as aggregate track candidates.")
            .Produces<SearchResultSnapshotDto<AggregateTrackCandidateDto>>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/jobs/{jobId:guid}/results/aggregate-albums", (Guid jobId, EngineSupervisor supervisor) =>
        {
            try
            {
                var results = supervisor.GetAggregateAlbumResults(jobId);
                return results != null ? Results.Ok(results) : Results.NotFound();
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Search Results")
            .WithSummary("Gets aggregate album candidates.")
            .Produces<SearchResultSnapshotDto<AggregateAlbumCandidateDto>>()
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/jobs/{jobId:guid}/results/aggregate-albums/project", (Guid jobId, AggregateAlbumProjectionRequestDto request, EngineSupervisor supervisor) =>
        {
            try
            {
                var results = supervisor.GetAggregateAlbumResults(jobId, request);
                return results != null ? Results.Ok(results) : Results.NotFound();
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Search Results")
            .WithSummary("Projects search results as aggregate album candidates.")
            .Produces<SearchResultSnapshotDto<AggregateAlbumCandidateDto>>()
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/jobs/{jobId:guid}/retrieve-folder", async (
            Guid jobId,
            RetrieveFolderRequestDto request,
            EngineSupervisor supervisor,
            CancellationToken ct) =>
        {
            try
            {
                var summary = await supervisor.StartRetrieveFolderAsync(jobId, request, ct);
                return summary != null
                    ? Results.Accepted($"/api/jobs/{summary.JobId}", summary)
                    : Results.NotFound();
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Follow-up Jobs")
            .WithSummary("Starts a folder retrieval job for a selected album result folder.")
            .WithDescription("Retrieves the full remote folder contents for a selected folder result. Search responses can omit child items that did not match the original query.")
            .Produces<JobSummaryDto>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest);

        app.MapPost("/api/jobs/{jobId:guid}/downloads/files", async (
            Guid jobId,
            StartFileDownloadsRequestDto request,
            EngineSupervisor supervisor,
            CancellationToken ct) =>
        {
            try
            {
                var summaries = await supervisor.StartFileDownloadsAsync(jobId, request, ct);
                return summaries != null
                    ? Results.Accepted($"/api/jobs/{jobId}", summaries)
                    : Results.NotFound();
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Follow-up Jobs")
            .WithSummary("Starts one or more file download jobs from selected search result files.")
            .WithDescription("The source search job identifies where the candidate refs came from. Per-download settings belong in the request options, not in the original search job.")
            .Produces<IReadOnlyList<JobSummaryDto>>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest);

        app.MapPost("/api/jobs/{jobId:guid}/downloads/folder", async (
            Guid jobId,
            StartFolderDownloadRequestDto request,
            EngineSupervisor supervisor,
            CancellationToken ct) =>
        {
            try
            {
                var summary = await supervisor.StartFolderDownloadAsync(jobId, request, ct);
                return summary != null
                    ? Results.Accepted($"/api/jobs/{summary.JobId}", summary)
                    : Results.NotFound();
            }
            catch (Exception ex) when (TryCreateBadRequest(ex, out _))
            {
                return BadRequest(ex);
            }
        })
            .WithTags("Follow-up Jobs")
            .WithSummary("Starts an album/folder download job from a selected folder result.")
            .WithDescription("The source search job identifies where the folder ref came from. Per-download settings belong in the request options, not in the original search job.")
            .Produces<JobSummaryDto>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest);

        app.MapPost("/api/jobs/{jobId:guid}/manual/complete", async (Guid jobId, EngineSupervisor supervisor) =>
        {
            return await supervisor.CompleteManualSelectionAsync(jobId)
                ? Results.Accepted($"/api/jobs/{jobId}")
                : Results.NotFound();
        })
            .WithTags("Jobs")
            .WithSummary("Completes a manual-selection job without starting additional downloads.")
            .WithDescription("Use this when a DownloadBehavior.Manual job reached AwaitingSelection and the caller wants to close the manual step without resuming the job.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/jobs/{jobId:guid}/manual/skip", async (Guid jobId, EngineSupervisor supervisor) =>
        {
            return await supervisor.SkipManualSelectionAsync(jobId)
                ? Results.Accepted($"/api/jobs/{jobId}")
                : Results.NotFound();
        })
            .WithTags("Jobs")
            .WithSummary("Skips a manual-selection job without starting additional downloads.")
            .WithDescription("Use this when a DownloadBehavior.Manual job reached AwaitingSelection and the caller wants to record an explicit user skip.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/jobs/{jobId:guid}/cancel", (Guid jobId, EngineSupervisor supervisor) =>
        {
            return supervisor.CancelJob(jobId)
                ? Results.Accepted($"/api/jobs/{jobId}")
                : Results.NotFound();
        })
            .WithTags("Jobs")
            .WithSummary("Cancels a job when cancellation is available.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/workflows/{workflowId:guid}/jobs/display/{displayId:int}/cancel", (Guid workflowId, int displayId, EngineSupervisor supervisor) =>
        {
            return supervisor.CancelJobByDisplayId(workflowId, displayId)
                ? Results.Accepted($"/api/workflows/{workflowId}")
                : Results.NotFound();
        })
            .WithTags("Workflows")
            .WithSummary("Cancels a workflow job by display id.")
            .WithDescription("Convenience endpoint for CLI-style cancellation prompts. Normal GUI clients should prefer AvailableActions on known job ids.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/jobs/{jobId:guid}/next-candidate", (Guid jobId, EngineSupervisor supervisor) =>
        {
            return supervisor.TryNextCandidate(jobId)
                ? Results.Accepted($"/api/jobs/{jobId}")
                : Results.NotFound();
        })
            .WithTags("Jobs")
            .WithSummary("Tries the next candidate for an active job download.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/jobs/{jobId:guid}/retry", (Guid jobId, EngineSupervisor supervisor) =>
        {
            return supervisor.RetryJob(jobId)
                ? Results.Accepted($"/api/jobs/{jobId}")
                : Results.NotFound();
        })
            .WithTags("Jobs")
            .WithSummary("Retries a terminal job using its prepared execution context.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/workflows/{workflowId:guid}/jobs/display/{displayId:int}/next-candidate", (Guid workflowId, int displayId, EngineSupervisor supervisor) =>
        {
            return supervisor.TryNextCandidateByDisplayId(workflowId, displayId)
                ? Results.Accepted($"/api/workflows/{workflowId}")
                : Results.NotFound();
        })
            .WithTags("Workflows")
            .WithSummary("Tries the next candidate for an active job download by display id.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/jobs/extract", async (SubmitExtractJobRequestDto request, EngineSupervisor supervisor, CancellationToken ct) =>
            await SubmitJobAsync(() => supervisor.SubmitExtractJobAsync(request, ct)))
            .WithTags("Job Submission")
            .WithSummary("Submits an input extraction job.")
            .Produces<JobSummaryDto>(StatusCodes.Status202Accepted)
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest);

        app.MapPost("/api/jobs/search", async (SubmitSearchJobRequestDto request, EngineSupervisor supervisor, CancellationToken ct) =>
            await SubmitJobAsync(() => supervisor.SubmitSearchJobAsync(request, ct)))
            .WithTags("Job Submission")
            .WithSummary("Submits a generic Soulseek search job.")
            .WithDescription("Search jobs are discovery-oriented. They store raw Soulseek results; use projection endpoints to view those results as files, album folders, or aggregate candidates, then use follow-up download endpoints for selected refs.")
            .Produces<JobSummaryDto>(StatusCodes.Status202Accepted)
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest);

        app.MapPost("/api/jobs/search/tracks", async (SubmitTrackSearchJobRequestDto request, EngineSupervisor supervisor, CancellationToken ct) =>
            await SubmitJobAsync(() => supervisor.SubmitTrackSearchJobAsync(request, ct)))
            .WithTags("Job Submission")
            .WithSummary("Submits a track search job.")
            .WithDescription("Track search jobs are suitable for exploratory pick-then-download UIs: inspect projected file candidates from the result endpoints, then start follow-up downloads from selected refs.")
            .Produces<JobSummaryDto>(StatusCodes.Status202Accepted)
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest);

        app.MapPost("/api/jobs/search/albums", async (SubmitAlbumSearchJobRequestDto request, EngineSupervisor supervisor, CancellationToken ct) =>
            await SubmitJobAsync(() => supervisor.SubmitAlbumSearchJobAsync(request, ct)))
            .WithTags("Job Submission")
            .WithSummary("Submits an album search job.")
            .WithDescription("Album search jobs are suitable for exploratory pick-then-download UIs: inspect projected folder candidates from the result endpoints, then start a follow-up folder download from the selected ref.")
            .Produces<JobSummaryDto>(StatusCodes.Status202Accepted)
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest);

        app.MapPost("/api/jobs/downloads/song", async (SubmitSongJobRequestDto request, EngineSupervisor supervisor, CancellationToken ct) =>
            await SubmitJobAsync(() => supervisor.SubmitSongJobAsync(request, ct)))
            .WithTags("Job Submission")
            .WithSummary("Submits a single-file download job.")
            .WithDescription("Use DownloadBehavior.Automatic for normal transfer jobs. Use DownloadBehavior.Manual when the job should pause at AwaitingSelection for caller approval/selection before resuming the same job.")
            .Produces<JobSummaryDto>(StatusCodes.Status202Accepted)
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest);

        app.MapPost("/api/jobs/downloads/album", async (SubmitAlbumJobRequestDto request, EngineSupervisor supervisor, CancellationToken ct) =>
            await SubmitJobAsync(() => supervisor.SubmitAlbumJobAsync(request, ct)))
            .WithTags("Job Submission")
            .WithSummary("Submits an album/folder download job.")
            .WithDescription("Use DownloadBehavior.Automatic for normal transfer jobs. Use DownloadBehavior.Manual when the job should pause at AwaitingSelection for caller approval/selection before resuming the same job.")
            .Produces<JobSummaryDto>(StatusCodes.Status202Accepted)
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest);

        app.MapPost("/api/jobs/aggregate/tracks", async (SubmitAggregateJobRequestDto request, EngineSupervisor supervisor, CancellationToken ct) =>
            await SubmitJobAsync(() => supervisor.SubmitAggregateJobAsync(request, ct)))
            .WithTags("Job Submission")
            .WithSummary("Submits an aggregate track search job.")
            .WithDescription("Aggregate jobs can download automatically or, with DownloadBehavior.Manual, pause after candidate grouping so the caller can choose which child downloads to resume.")
            .Produces<JobSummaryDto>(StatusCodes.Status202Accepted)
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest);

        app.MapPost("/api/jobs/aggregate/albums", async (SubmitAlbumAggregateJobRequestDto request, EngineSupervisor supervisor, CancellationToken ct) =>
            await SubmitJobAsync(() => supervisor.SubmitAlbumAggregateJobAsync(request, ct)))
            .WithTags("Job Submission")
            .WithSummary("Submits an aggregate album search job.")
            .WithDescription("Aggregate album jobs can download automatically or, with DownloadBehavior.Manual, pause after bucket projection so the caller can choose which child downloads to resume.")
            .Produces<JobSummaryDto>(StatusCodes.Status202Accepted)
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest);

        app.MapPost("/api/jobs/lists", async (SubmitJobListRequestDto request, EngineSupervisor supervisor, CancellationToken ct) =>
            await SubmitJobAsync(() => supervisor.SubmitJobListAsync(request, ct)))
            .WithTags("Job Submission")
            .WithSummary("Submits a job list from draft child jobs.")
            .WithDescription("Job drafts are submission payloads only. Submitted children appear as normal runtime jobs in subsequent job/workflow snapshots.")
            .Produces<JobSummaryDto>(StatusCodes.Status202Accepted)
            .Produces<ApiErrorDto>(StatusCodes.Status400BadRequest);

        app.MapGet("/api/workflows", (EngineStateStore stateStore) => Results.Ok(stateStore.GetWorkflows()))
            .WithTags("Workflows")
            .WithSummary("Lists known workflows.")
            .Produces<IReadOnlyList<WorkflowSummaryDto>>();

        app.MapGet("/api/workflows/{workflowId:guid}", (Guid workflowId, bool? includeAll, EngineStateStore stateStore) =>
        {
            var workflow = stateStore.GetWorkflow(workflowId, includeAll == true);
            return workflow != null ? Results.Ok(workflow) : Results.NotFound();
        })
            .WithTags("Workflows")
            .WithSummary("Gets a workflow snapshot by id.")
            .WithDescription("Default results contain only execution roots where ParentJobId is null. Set includeAll=true for a flat list of every workflow job. Use /tree for the same jobs grouped by ParentJobId.")
            .Produces<WorkflowDetailDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/workflows/{workflowId:guid}/tree", (Guid workflowId, EngineStateStore stateStore) =>
        {
            var workflow = stateStore.GetWorkflowTree(workflowId);
            return workflow != null ? Results.Ok(workflow) : Results.NotFound();
        })
            .WithTags("Workflows")
            .WithSummary("Gets the execution tree for a workflow.")
            .WithDescription("This tree is built only from ParentJobId relationships. Follow-up jobs started from search results remain workflow roots and expose SourceJobId instead.")
            .Produces<WorkflowTreeDto>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/workflows/{workflowId:guid}/cancel", (Guid workflowId, EngineSupervisor supervisor) =>
        {
            int cancelled = supervisor.CancelWorkflow(workflowId);
            return cancelled > 0
                ? Results.Accepted($"/api/workflows/{workflowId}", new CancelWorkflowResponseDto(cancelled))
                : Results.NotFound();
        })
            .WithTags("Workflows")
            .WithSummary("Cancels cancellable jobs in a workflow.")
            .Produces<CancelWorkflowResponseDto>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound);

        app.MapHub<ServerEventHub>("/api/events");
    }

    private static bool RequiresSessionToken(PathString path)
    {
        if (!path.StartsWithSegments("/api/v1", StringComparison.Ordinal))
            return false;

        return !path.StartsWithSegments("/api/v1/system/health", StringComparison.Ordinal);
    }

    private static Task<PlayerStateDto> ToPlayerStateDtoAsync(
        PlaybackCoordinator player,
        SockseekDbContext dbContext,
        LocalArtworkCache artworkCache,
        CancellationToken cancellationToken)
        => ToPlayerStateDtoAsync(player, dbContext, artworkCache, player.Snapshot, cancellationToken);

    private static ProviderCapabilityDto ToProviderCapabilityDto(ProviderCapabilities provider)
        => new(
            provider.ProviderId,
            provider.DisplayName,
            provider.SupportsPlaylistImport,
            provider.SupportsMetadataLookup,
            provider.SupportsAccountConnection,
            provider.SupportsPublicUrlImport,
            Enum.GetValues<PlaylistProviderCapabilities>()
                .Where(capability => capability != PlaylistProviderCapabilities.None && provider.Supports(capability))
                .Select(capability => capability.ToString())
                .ToArray());

    private static ExternalAccountDto ToExternalAccountDto(ExternalAccountEntity account)
        => new(
            account.Id,
            ToProviderId(account.Provider),
            account.ExternalUserId,
            account.DisplayName,
            ToExternalAccountStatus(account.Status),
            account.LastAuthorizedAtUtc);

    private static ExternalPlaylistSummaryDto ToExternalPlaylistSummaryDto(ExternalPlaylistSummary playlist)
        => new(
            playlist.ProviderId,
            playlist.ExternalPlaylistId,
            playlist.Name,
            playlist.Url,
            playlist.ItemCount,
            playlist.LastModifiedAtUtc);

    private static PlaylistSummaryDto ToPlaylistSummaryDto(PlaylistSummaryRecord playlist)
        => new(
            playlist.PlaylistId,
            playlist.Name,
            playlist.ImportMode,
            playlist.ProviderId,
            playlist.ExternalPlaylistId,
            playlist.ExternalUrl,
            playlist.CreatedAtUtc,
            playlist.UpdatedAtUtc,
            playlist.LastSyncedAtUtc,
            ToPlaylistResolutionSummaryDto(playlist.Resolution));

    private static PlaylistDetailDto ToPlaylistDetailDto(PlaylistDetailRecord playlist)
        => new(
            playlist.PlaylistId,
            playlist.Name,
            playlist.ImportMode,
            playlist.ProviderId,
            playlist.ExternalPlaylistId,
            playlist.ExternalUrl,
            playlist.CreatedAtUtc,
            playlist.UpdatedAtUtc,
            playlist.LastSyncedAtUtc,
            ToPlaylistResolutionSummaryDto(playlist.Resolution),
            playlist.Items.Select(ToPlaylistItemDto).ToArray());

    private static PlaylistItemDto ToPlaylistItemDto(PlaylistItemRecord item)
        => new(
            item.PlaylistItemId,
            item.Position,
            item.ProviderItemId,
            item.CanonicalTrackId,
            item.Status,
            item.Title,
            item.Artists,
            item.Album,
            item.DurationMs,
            item.Isrc,
            item.MusicBrainzRecordingId,
            item.ExternalTrackId,
            item.ExternalUrl,
            item.ArtworkUrl,
            item.RemovedAtUtc);

    private static PlaylistResolutionSummaryDto ToPlaylistResolutionSummaryDto(PlaylistResolutionSummaryRecord summary)
        => new(
            summary.TotalItems,
            summary.AvailableLocalItems,
            summary.UnresolvedItems,
            summary.ReviewRequiredItems,
            summary.SearchingItems,
            summary.CandidateFoundItems,
            summary.DownloadingItems,
            summary.FailedItems,
            summary.SkippedItems,
            summary.RemovedItems);

    private static PlaylistDownloadSubmissionDto ToPlaylistDownloadSubmissionDto(PlaylistDownloadSubmissionRecord submission)
        => new(
            submission.PlaylistItemId,
            submission.WorkflowId,
            submission.EngineJobId);

    private static async Task<IReadOnlyList<PlaybackQueueItem>> GetAvailablePlaylistQueueItemsAsync(
        SockseekDbContext db,
        Guid playlistId,
        int? startAfterPosition,
        CancellationToken cancellationToken)
    {
        var rows = await db.PlaylistItems
            .AsNoTracking()
            .Where(item => item.PlaylistId == playlistId
                && item.RemovedAtUtc == null
                && item.Status == (int)PlaylistItemStatus.AvailableLocal
                && item.CanonicalTrackId.HasValue
                && (!startAfterPosition.HasValue || item.Position >= startAfterPosition.Value))
            .SelectMany(
                item => item.CanonicalTrack!.LocalMediaFiles
                    .Where(file => file.Availability == (int)LocalMediaAvailability.Available),
                (item, file) => new
                {
                    PlaylistItemId = item.Id,
                    item.Position,
                    item.ProviderItemId,
                    CanonicalTrackId = item.CanonicalTrackId!.Value,
                    LocalMediaFileId = file.Id,
                    Bitrate = file.Bitrate ?? 0,
                    SampleRate = file.SampleRate ?? 0,
                    BitDepth = file.BitDepth ?? 0,
                    file.Path,
                })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.PlaylistItemId)
            .Select(group => group
                .OrderByDescending(row => row.Bitrate)
                .ThenByDescending(row => row.SampleRate)
                .ThenByDescending(row => row.BitDepth)
                .ThenBy(row => row.Path)
                .First())
            .OrderBy(row => row.Position)
            .ThenBy(row => row.ProviderItemId)
            .Select(row => new PlaybackQueueItem(
                Guid.NewGuid(),
                row.CanonicalTrackId,
                row.LocalMediaFileId))
            .ToArray();
    }

    private static string ToProviderId(int provider)
        => Enum.IsDefined(typeof(ExternalProvider), provider)
            ? (ExternalProvider)provider switch
            {
                ExternalProvider.Spotify => ProviderIds.Spotify,
                ExternalProvider.YouTube => ProviderIds.YouTube,
                ExternalProvider.Bandcamp => ProviderIds.Bandcamp,
                ExternalProvider.MusicBrainz => ProviderIds.MusicBrainz,
                _ => ((ExternalProvider)provider).ToString().ToLowerInvariant(),
            }
            : $"unknown-{provider.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    private static ExternalProvider? ToExternalProvider(string providerId)
        => providerId switch
        {
            ProviderIds.Spotify => ExternalProvider.Spotify,
            ProviderIds.YouTube => ExternalProvider.YouTube,
            ProviderIds.Bandcamp => ExternalProvider.Bandcamp,
            ProviderIds.MusicBrainz => ExternalProvider.MusicBrainz,
            _ => null,
        };

    private static string ToExternalAccountStatus(int status)
        => Enum.IsDefined(typeof(ExternalAccountStatus), status)
            ? ((ExternalAccountStatus)status).ToString()
            : "Unknown";

    private static async Task<PlayerStateDto> ToSavedPlayerStateDtoAsync(
        PlaybackCoordinator player,
        SockseekDbContext dbContext,
        LocalArtworkCache artworkCache,
        PlaybackQueuePersistenceService queuePersistence,
        PlaybackSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await queuePersistence.SaveDefaultQueueAsync(cancellationToken);
        return await ToPlayerStateDtoAsync(player, dbContext, artworkCache, snapshot, cancellationToken);
    }

    private static async Task<PlayerStateDto> ToPlayerStateDtoAsync(
        PlaybackCoordinator player,
        SockseekDbContext dbContext,
        LocalArtworkCache artworkCache,
        PlaybackSnapshot snapshot,
        CancellationToken cancellationToken)
        => new(
            snapshot.State.ToString(),
            snapshot.CanonicalTrackId,
            snapshot.PlaylistItemId,
            snapshot.LocalMediaFileId,
            snapshot.Path,
            snapshot.ErrorMessage,
            Convert.ToInt64(snapshot.Position.TotalMilliseconds),
            snapshot.Volume,
            snapshot.IsMuted,
            ToPlayerQueueDto(player.Queue),
            await ResolveNowPlayingAsync(dbContext, artworkCache, snapshot, cancellationToken),
            ToPlayerBufferDto(snapshot.Buffer));

    private static PlayerBufferDto? ToPlayerBufferDto(PlaybackBufferSnapshot? buffer)
        => buffer == null
            ? null
            : new PlayerBufferDto(
                buffer.Status.ToString(),
                buffer.CanOpenMedia,
                Convert.ToInt64(buffer.BufferedUntil.TotalMilliseconds),
                buffer.SeekLimit is { } seekLimit ? Convert.ToInt64(seekLimit.TotalMilliseconds) : null,
                buffer.AvailableBytes,
                buffer.ExpectedBytes,
                buffer.DownloadBytesPerSecond,
                buffer.Reason);

    private static bool IsProgressivePlaybackEnabledForCodec(ServerOptions options, string extension)
        => options.ExperimentalProgressivePlayback
        && string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase);

    private static async Task<PlayerNowPlayingDto?> ResolveNowPlayingAsync(
        SockseekDbContext dbContext,
        LocalArtworkCache artworkCache,
        PlaybackSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.LocalMediaFileId is Guid localMediaFileId)
        {
            var file = await dbContext.LocalMediaFiles
                .AsNoTracking()
                .Include(localFile => localFile.CanonicalTrack)
                .SingleOrDefaultAsync(localFile => localFile.Id == localMediaFileId, cancellationToken);
            if (file != null)
                return await ToNowPlayingAsync(file, snapshot.Path, artworkCache, cancellationToken);
        }

        if (snapshot.CanonicalTrackId is Guid canonicalTrackId)
        {
            var track = await dbContext.CanonicalTracks
                .AsNoTracking()
                .Include(canonicalTrack => canonicalTrack.LocalMediaFiles)
                .SingleOrDefaultAsync(canonicalTrack => canonicalTrack.Id == canonicalTrackId, cancellationToken);
            if (track != null)
                return await ToNowPlayingAsync(track, snapshot.Path, artworkCache, cancellationToken);
        }

        return !string.IsNullOrWhiteSpace(snapshot.Path)
            ? new PlayerNowPlayingDto(
                Title: Path.GetFileNameWithoutExtension(snapshot.Path),
                Artist: null,
                AlbumTitle: null,
                DurationMs: null,
                Codec: null,
                ArtworkPath: await ResolveArtworkPathAsync(artworkCache, snapshot.Path, cancellationToken),
                Source: "path")
            : null;
    }

    private static async Task<PlayerNowPlayingDto> ToNowPlayingAsync(
        LocalMediaFileEntity file,
        string? path,
        LocalArtworkCache artworkCache,
        CancellationToken cancellationToken)
    {
        var track = file.CanonicalTrack;
        var localPath = NullIfEmpty(path) ?? file.Path;
        return new PlayerNowPlayingDto(
            Title: NullIfEmpty(track?.Title) ?? Path.GetFileNameWithoutExtension(localPath),
            Artist: NullIfEmpty(track?.Artist),
            AlbumTitle: NullIfEmpty(track?.AlbumTitle),
            DurationMs: ToLong(file.DurationMs ?? track?.DurationMs),
            Codec: NullIfEmpty(file.Codec),
            ArtworkPath: await ResolveArtworkPathAsync(artworkCache, localPath, cancellationToken),
            Source: "local_media_file");
    }

    private static async Task<PlayerNowPlayingDto> ToNowPlayingAsync(
        CanonicalTrackEntity track,
        string? path,
        LocalArtworkCache artworkCache,
        CancellationToken cancellationToken)
    {
        var bestFile = track.LocalMediaFiles
            .OrderByDescending(file => file.Bitrate ?? 0)
            .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var localPath = NullIfEmpty(path) ?? bestFile?.Path;
        return new PlayerNowPlayingDto(
            Title: NullIfEmpty(track.Title) ?? Path.GetFileNameWithoutExtension(localPath ?? string.Empty),
            Artist: NullIfEmpty(track.Artist),
            AlbumTitle: NullIfEmpty(track.AlbumTitle),
            DurationMs: ToLong(track.DurationMs ?? bestFile?.DurationMs),
            Codec: NullIfEmpty(bestFile?.Codec),
            ArtworkPath: await ResolveArtworkPathAsync(artworkCache, localPath, cancellationToken),
            Source: "canonical_track");
    }

    private static Task<string?> ResolveArtworkPathAsync(
        LocalArtworkCache artworkCache,
        string? path,
        CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(path)
            ? Task.FromResult<string?>(null)
            : artworkCache.ExtractAsync(path, cancellationToken);

    private static long? ToLong(int? value) => value;

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static PlayerQueueDto ToPlayerQueueDto(PlaybackQueueSnapshot queue)
        => new(
            queue.Items
                .Select(item => new PlayerQueueItemDto(
                    item.Id,
                    item.CanonicalTrackId,
                    item.LocalMediaFileId,
                    item.DownloadWorkflowId))
                .ToList(),
            queue.CurrentIndex,
            queue.RepeatMode.ToString(),
            queue.ShuffleEnabled,
            queue.ShuffleSeed,
            queue.PlaybackOrder.ToList());

    private static string GetCorrelationId(HttpContext context)
        => context.TraceIdentifier;

    private static async Task<IResult> SubmitJobAsync(Func<Task<JobSummaryDto>> submit)
    {
        try
        {
            var summary = await submit();
            return Results.Accepted($"/api/jobs/{summary.JobId}", summary);
        }
        catch (Exception ex) when (TryCreateBadRequest(ex, out _))
        {
            return BadRequest(ex);
        }
    }

    private static IResult BadRequest(Exception ex)
    {
        TryCreateBadRequest(ex, out var error);
        Sockseek.Core.SockseekLog.Daemon.Warn($"Bad request: {error}");
        return Results.BadRequest(new ApiErrorDto(error));
    }

    private static bool TryCreateBadRequest(Exception ex, out string error)
    {
        error = ex.Message;
        return ex is ArgumentException
            || ex.Message.StartsWith("Input error:", StringComparison.Ordinal);
    }
}
