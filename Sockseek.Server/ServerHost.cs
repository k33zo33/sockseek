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
using Sockseek.Infrastructure;
using Sockseek.Infrastructure.LocalLibrary;
using Sockseek.Infrastructure.Persistence;
using Sockseek.Infrastructure.Persistence.Entities;
using Sockseek.Infrastructure.Security;
using Sockseek.Integrations.Abstractions;
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
        builder.Services.AddSingleton<OAuthPkceCoordinator>();
        builder.Services.AddSingleton<ISecretStore>(sp =>
            new WindowsDpapiSecretStore(ResolveSecretStoreDirectory(sp.GetRequiredService<IOptions<ServerOptions>>().Value)));
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
        builder.Services.AddScoped<LocalPlaybackSourceResolver>();
        builder.Services.AddSingleton<IPlaybackSourceResolver, ScopedPlaybackSourceResolver>();
        builder.Services.AddSingleton<IMediaEngine, LibVlcMediaEngine>();
        builder.Services.AddSingleton<PlaybackCoordinator>();
        builder.Services.AddSingleton<PlaybackQueuePersistenceService>();
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
