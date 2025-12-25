/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using Listenarr.Api.Services;
using Listenarr.Api.Services.Search;
using Listenarr.Api.Services.Search.Filters;
using Listenarr.Api.Services.Search.Strategies;
using Listenarr.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Middleware;
using System.Net;
using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Serilog;
using Serilog.Events;
using Polly;
using Polly.Extensions.Http;
using Listenarr.Api.Extensions;
using Listenarr.Infrastructure.Extensions;

// Check for special CLI helpers before building the web host
// Pass a non-null args array to satisfy nullable analysis
var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());

// Configure Serilog for structured logging, file rotation and SignalR broadcasting
var logFilePath = Path.Combine(builder.Environment.ContentRootPath, "config", "logs", "listenarr-.log");
var signalRSink = new SignalRLogSink();

// Industry-standard defaults:
// - Application logs at Information
// - Third-party and framework logs (Microsoft/System) at Warning
// - EF Core DB command logging elevated to Warning by default (can be lowered to Debug for troubleshooting)
Log.Logger = new Serilog.LoggerConfiguration()
    .Enrich.FromLogContext()
    // Use explicit properties to avoid optional enrichers that may not be present in all builds
    .Enrich.WithProperty("Machine", Environment.MachineName)
    .Enrich.WithProperty("ProcessId", Environment.ProcessId)
    .Enrich.WithProperty("Application", "Listenarr.Api")
    .MinimumLevel.Information()
    // Framework and system noise should be at Warning by default
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    // EF Core: keep DB command messages higher than app logs; changeable via configuration when needed
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    // Enable debug logging for Transmission adapter to troubleshoot RPC issues
    .MinimumLevel.Override("Listenarr.Api.Services.Adapters.TransmissionAdapter", LogEventLevel.Debug)
    // Console sink for developer-friendly output (includes SourceContext for quick tracing)
    .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    // Primary file sink with daily rolling and structured JSON compatible output template
    .WriteTo.File(
        logFilePath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Sink(signalRSink)
    .CreateLogger();

// Use Serilog for logging
builder.Host.UseSerilog();

// Configure URLs to listen on port 5000 (standard ASP.NET Core port) - can be overridden by --urls
if (!args?.Any(arg => arg.StartsWith("--urls")) ?? true)
{
    builder.WebHost.UseUrls("http://*:5000");
}

// Configure logging is now handled by Serilog above

// Add services to the container.
// If running as an integration test host, allow the test-side partial to apply any
// additional registrations (for example AddListenarrPersistence so IDbContextFactory<>
// is available to hosted/background services during tests).
if (builder.Environment.IsEnvironment("Testing"))
{
    ApplyTestHostPatches(builder);
}
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings instead of integers for better frontend compatibility
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Add SignalR for real-time updates
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        // Serialize enums as strings for SignalR messages too
        options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Add in-memory cache for metadata prefetch / reuse
builder.Services.AddMemoryCache();

// Add HTTP client for external API calls with decompression support
builder.Services.AddHttpClient<IAudibleMetadataService, AudibleMetadataService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

// Add Amazon metadata service (delegates to AudibleMetadataService for shared logic)
builder.Services.AddScoped<IAmazonMetadataService, AmazonMetadataService>();

// Add HTTP client for Audimeta service
builder.Services.AddHttpClient<AudimetaService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

// Add HTTP client for Audnexus service
builder.Services.AddHttpClient<IAudnexusService, AudnexusService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

// Metadata routing across providers
builder.Services.AddScoped<IAudiobookMetadataService, AudiobookMetadataService>();

// Add metadata converters helper
builder.Services.AddScoped<MetadataConverters>();
builder.Services.AddScoped<MetadataMerger>();
builder.Services.AddScoped<SearchProgressReporter>();

// Add search result filters
builder.Services.AddScoped<ISearchResultFilter, KindleEditionFilter>();
builder.Services.AddScoped<ISearchResultFilter, AudiobookOnlyFilter>();
builder.Services.AddScoped<ISearchResultFilter, PromotionalTitleFilter>();
builder.Services.AddScoped<ISearchResultFilter, ProductLikeTitleFilter>();
builder.Services.AddScoped<ISearchResultFilter, MissingInformationFilter>();
builder.Services.AddScoped<SearchResultFilterPipeline>();

// Add metadata fetching strategies
builder.Services.AddScoped<IMetadataStrategy, AudimetaStrategy>();
builder.Services.AddScoped<IMetadataStrategy, AudnexusStrategy>();
builder.Services.AddScoped<MetadataStrategyCoordinator>();

// Add ASIN candidate collector
builder.Services.AddScoped<AsinCandidateCollector>();

// Add ASIN enricher
builder.Services.AddScoped<AsinEnricher>();

// Add fallback scraper
builder.Services.AddScoped<FallbackScraper>();

// Add search result scorer
builder.Services.AddScoped<SearchResultScorer>();

// Add ASIN search handler
builder.Services.AddScoped<AsinSearchHandler>();

// Audible integration removed: AudibleApiService registration omitted

// Add default HTTP client for other services
builder.Services.AddHttpClient();

// Add named HttpClient for download operations (qBittorrent, Transmission, etc.)
// Prevents socket exhaustion by reusing connections
builder.Services.AddHttpClient("DownloadClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        UseCookies = false // Cookies handled per-request for multi-tenant scenarios
    })
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, duration) =>
            {
                Log.Logger.Warning("[CIRCUIT BREAKER] Download client circuit opened due to {Reason}. Breaking for {Seconds}s", outcome.Exception?.Message ?? "policy trigger", duration.TotalSeconds);
            },
            onReset: () =>
            {
                Log.Logger.Information("[CIRCUIT BREAKER] Download client circuit reset - service recovered");
            }
        ))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Log.Logger.Information("[RETRY] Download client retry attempt {Attempt} after {Delay}s delay", retryAttempt, timespan.TotalSeconds);
            }
        ));

// Bind download client definitions from configuration and expose via IOptions
builder.Services.Configure<Listenarr.Api.Services.Adapters.DownloadClientsOptions>(builder.Configuration.GetSection("DownloadClients"));

// Validate download client configuration at startup, surface errors early
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<Listenarr.Api.Services.Adapters.DownloadClientsOptions>, Listenarr.Api.Services.Adapters.DownloadClientsOptionsValidator>();

// Register named HttpClients for each adapter type so adapter implementations can request the appropriately-configured client.
// qbittorrent
builder.Services.AddHttpClient("qbittorrent")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        UseCookies = false
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, duration) =>
            {
                Log.Logger.Warning("[CIRCUIT BREAKER] qbittorrent client circuit opened due to {Reason}. Breaking for {Seconds}s", outcome.Exception?.Message ?? "policy trigger", duration.TotalSeconds);
            },
            onReset: () =>
            {
                Log.Logger.Information("[CIRCUIT BREAKER] qbittorrent client circuit reset - service recovered");
            }
        ))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Log.Logger.Information("[RETRY] qbittorrent client retry attempt {Attempt} after {Delay}s delay", retryAttempt, timespan.TotalSeconds);
            }
        ));

// transmission
builder.Services.AddHttpClient("transmission")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        UseCookies = false
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30)))
    .AddPolicyHandler(HttpPolicyExtensions.HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// sabnzbd
builder.Services.AddHttpClient("sabnzbd")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        UseCookies = false
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30)))
    .AddPolicyHandler(HttpPolicyExtensions.HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// nzbget
builder.Services.AddHttpClient("nzbget")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        UseCookies = false
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30)))
    .AddPolicyHandler(HttpPolicyExtensions.HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// Adapter factory resolution is provided by `IDownloadClientAdapterFactory`.

// Add named HttpClient for direct downloads (DDL)
builder.Services.AddHttpClient("DirectDownload")
    .ConfigureHttpClient(client =>
    {


        // Allow up to 2 hours for large file downloads
        client.Timeout = TimeSpan.FromHours(2);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .Or<TaskCanceledException>()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromMinutes(1),
            onBreak: (outcome, duration) =>
            {
                Log.Logger.Warning("[CIRCUIT BREAKER] Direct download circuit opened. Breaking for {Minutes}m", duration.TotalMinutes);
            },
            onReset: () =>
            {
                Log.Logger.Information("[CIRCUIT BREAKER] Direct download circuit reset");
            }
        ))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .Or<TaskCanceledException>()
        .WaitAndRetryAsync(
            retryCount: 2,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
        ));

// Register our custom services
// Compute an absolute path for the SQLite file based on the content root so
// the published exe will create/use the intended config/database path even
// when the working directory differs.
// Compute default SQLite DB path (config/database/listenarr.db) relative to content root.
var sqliteDbPath = Path.Combine(builder.Environment.ContentRootPath, "config", "database", "listenarr.db");
// Ensure directory exists at startup so EF migrations can create the DB file there
var sqliteDbDir = Path.GetDirectoryName(sqliteDbPath);
if (!string.IsNullOrEmpty(sqliteDbDir) && !Directory.Exists(sqliteDbDir))
{
    Directory.CreateDirectory(sqliteDbDir);
}

// Register persistence (DbContextFactory + compatibility DbContext + repositories) via extension
builder.Services.AddListenarrPersistence(builder.Configuration, sqliteDbPath);

// Register consolidated HttpClient policies and common typed clients
builder.Services.AddListenarrHttpClients(builder.Configuration);

// Register adapters and related options/validators
builder.Services.AddListenarrAdapters(builder.Configuration);

// Register infrastructure implementations (repositories live in the Infrastructure project)
builder.Services.AddListenarrInfrastructure();
// Register application-level services (moved from Program.cs to keep startup focused)
builder.Services.AddListenarrAppServices(builder.Configuration);
// Register hosted/background services (moved from Program.cs)
builder.Services.AddListenarrHostedServices(builder.Configuration);

// Typed HttpClients with automatic decompression for scraping services
builder.Services.AddHttpClient<IAmazonSearchService, AmazonSearchService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    // Treat Forbidden/TooManyRequests/ServiceUnavailable as transient-handled results
    // Retry a few times with exponential backoff before the circuit-breaker sees the failure
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
    .OrResult(r => r.StatusCode == HttpStatusCode.Forbidden
                     || r.StatusCode == (HttpStatusCode)429
                     || r.StatusCode == HttpStatusCode.ServiceUnavailable)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                var reason = outcome.Result != null ? $"HTTP {(int)outcome.Result.StatusCode}" : outcome.Exception?.Message;
                Log.Logger.Information("[RETRY] Amazon search retry attempt {Attempt} due to {Reason}. Waiting {Delay}s", retryAttempt, reason, timespan.TotalSeconds);
            }))
    // Circuit-breaker: raise threshold slightly to avoid tripping on short throttling bursts
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
    .OrResult(r => r.StatusCode == HttpStatusCode.Forbidden
                     || r.StatusCode == (HttpStatusCode)429
                     || r.StatusCode == HttpStatusCode.ServiceUnavailable)
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 6,
            durationOfBreak: TimeSpan.FromMinutes(2),
            onBreak: (outcome, duration) =>
            {
                var reason = outcome.Result != null ? $"HTTP {(int)outcome.Result.StatusCode}" : outcome.Exception?.Message ?? "policy trigger";
                Log.Logger.Warning("[CIRCUIT BREAKER] Amazon search circuit opened due to {Reason}. Breaking for {Minutes}m", reason, duration.TotalMinutes);
            },
            onReset: () =>
            {
                Log.Logger.Information("[CIRCUIT BREAKER] Amazon search circuit reset");
            }
        ));

builder.Services.AddHttpClient<IAudibleSearchService, AudibleSearchService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
            .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 4,
            durationOfBreak: TimeSpan.FromMinutes(2),
            onBreak: (outcome, duration) =>
            {
                Log.Logger.Warning("[CIRCUIT BREAKER] Audible search circuit opened. Breaking for {Minutes}m", duration.TotalMinutes);
            },
            onReset: () =>
            {
                Log.Logger.Information("[CIRCUIT BREAKER] Audible search circuit reset");
            }
        ))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(1)));

// External request options (Prefer US domain / optional US proxy)
builder.Services.Configure<Listenarr.Api.Services.ExternalRequestOptions>(builder.Configuration.GetSection("ExternalRequests"));

// Named HttpClient for US-origin requests (can be configured to use a proxy)
builder.Services.AddHttpClient("us").ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    };

    try
    {
        var section = builder.Configuration.GetSection("ExternalRequests");
        var useProxy = section.GetValue<bool>("UseUsProxy");
        if (useProxy)
        {
            var host = section.GetValue<string>("UsProxyHost");
            var port = section.GetValue<int>("UsProxyPort");
            if (!string.IsNullOrWhiteSpace(host) && port > 0)
            {
                var proxy = new WebProxy(host, port);
                var user = section.GetValue<string>("UsProxyUsername");
                var pass = section.GetValue<string>("UsProxyPassword");
                if (!string.IsNullOrWhiteSpace(user))
                    proxy.Credentials = new NetworkCredential(user, pass ?? string.Empty);
                handler.Proxy = proxy;
                handler.UseProxy = true;
            }
        }
    }
    catch (Exception ex)
    {
        Log.Logger.Warning("[WARNING] Failed to configure proxy settings: {Message}", ex.Message);
    }

    return handler;
});

// Read Playwright enablement flag from config (default true)
var playwrightEnabled = builder.Configuration.GetValue<bool>("Playwright:Enabled", true);

// Register Playwright services only when enabled in configuration
if (playwrightEnabled)
{
    // Register Playwright page fetcher for JS-rendered pages and bot-workarounds
    builder.Services.AddSingleton<Listenarr.Api.Services.IPlaywrightPageFetcher, Listenarr.Api.Services.PlaywrightPageFetcher>();

    // Playwright install status and background installer
    builder.Services.AddSingleton<Listenarr.Api.Services.PlaywrightInstallStatus>();
    builder.Services.AddHostedService<Listenarr.Api.Services.PlaywrightInstallBackgroundService>();
    builder.Services.AddSingleton<Listenarr.Api.Services.IPlaywrightInstaller, Listenarr.Api.Services.PlaywrightInstaller>();
}
else
{
    Log.Logger.Information("Playwright integration is disabled via configuration; skipping Playwright service registration.");
}

// CORS is handled by reverse proxy (nginx, Traefik, Caddy, etc.)
// Only add CORS support for local development
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevOnly",
            policy =>
            {
                policy.WithOrigins(
                        "http://localhost:5173",
                        "https://localhost:5173",
                        "http://127.0.0.1:5173",
                        "https://127.0.0.1:5173"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials(); // Required for SignalR
            });
    });
}

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Try to include XML comments if available
    try
    {
        var xmlFile = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + ".xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }
    catch (Exception ex)
    {
        Log.Logger.Warning("[WARNING] Failed to include XML comments in Swagger: {Message}", ex.Message);
    }
    // Use full type names for schema Ids (replace '+' from nested types with '.') to
    // avoid collisions between nested controller DTOs and top-level DTOs that share
    // the same simple type name (e.g. TranslatePathRequest).
    options.CustomSchemaIds(type => (type.FullName ?? type.Name).Replace('+', '.'));
});

// Authentication: Session-based (default)
// No additional authentication scheme configuration needed for sessions

// Configure forwarded headers - required for apps behind reverse proxy
// This allows the app to see the real client IP and original protocol (HTTP/HTTPS)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Only forward the essential headers needed for proper operation
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Trust any proxy since this app runs in a controlled Docker/reverse proxy environment
    // Users are responsible for configuring their reverse proxy securely
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Antiforgery (CSRF) protection for SPA: expect token in X-XSRF-TOKEN header
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

// During local development we often run the frontend on a different port via Vite
// and use plain HTTP. Ensure antiforgery cookie can be set in that scenario by
// relaxing the SecurePolicy and SameSite settings when running in Development.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-XSRF-TOKEN";
        // Allow the antiforgery cookie to be sent over plain HTTP during local dev
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.None;
        // During local development the frontend often runs on a different origin
        // (Vite dev server). Use SameSite=Lax so the browser will accept the
        // cookie for same-site requests to the Vite dev server while avoiding
        // the requirement to set Secure (which would require HTTPS). In our
        // setup the dev server proxies /api requests, so Lax is sufficient and
        // more compatible with local HTTP development.
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    });
}

// Persist Data Protection keys to disk so antiforgery tokens/cookies remain valid
// across process restarts and between instances during local development.
// This avoids issues where tokens are protected with an ephemeral key ring and
// cannot be validated later.
{
    var keyDir = Path.Combine(builder.Environment.ContentRootPath, "config", "dataprotection-keys");
    if (!System.IO.Directory.Exists(keyDir)) System.IO.Directory.CreateDirectory(keyDir);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new System.IO.DirectoryInfo(keyDir))
        .SetApplicationName("Listenarr");
}

var app = builder.Build();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Ensure the database directory exists (use the absolute path computed above)
    string dbFullPath = sqliteDbPath;
    string? dbDirectory = Path.GetDirectoryName(dbFullPath);
    if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
    {
        Directory.CreateDirectory(dbDirectory);
    }

    try
    {
        logger.LogInformation("Checking EF Core migrations (available/applied/pending)...");

        try
        {
            var available = context.Database.GetMigrations().ToList();
            var applied = context.Database.GetAppliedMigrations().ToList();
            var pending = context.Database.GetPendingMigrations().ToList();

            logger.LogInformation("Available migrations: {Count}", available.Count);
            foreach (var m in available)
            {
                logger.LogInformation("  - {Migration}", m);
            }

            logger.LogInformation("Applied migrations: {Count}", applied.Count);
            foreach (var m in applied)
            {
                logger.LogInformation("  - {Migration}", m);
            }

            logger.LogInformation("Pending migrations: {Count}", pending.Count);
            foreach (var m in pending)
            {
                logger.LogInformation("  - {Migration}", m);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enumerate EF Core migrations before migrating");
        }

        logger.LogInformation("Applying database migrations...");
        // Apply any pending migrations (including AddDefaultMetadataSources which adds Audimeta and Audnexus)
        context.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully");

        // Apply SQLite PRAGMA settings after database is created
        SqlitePragmaInitializer.ApplyPragmas(context);
        logger.LogInformation("SQLite pragmas applied successfully");

        // NOTE: Automatic schema ALTERs were intentionally removed from startup.
        // Schema changes should be applied by EF migrations. See Migration:
        // Migrations/20251125103000_AddDownloadFinalizationSettingsToApplicationSettings.cs
    }
    catch (Exception ex)
    {
        // Migrations can fail when the database file exists but is missing expected
        // tables (for example, when an older DB file was copied into the publish
        // folder). In that case try the cheaper EnsureCreated() path which will
        // create tables for the current model if no __EFMigrationsHistory table
        // exists. If EnsureCreated() succeeds, log the fact and continue. If it
        // fails as well, log full details for debugging but continue so the host
        // can start (tests may override DbContext configuration).
        logger.LogError(ex, "Error during database migration attempt. Will try EnsureCreated() fallback.");

        try
        {
            logger.LogInformation("Attempting EnsureCreated() as a fallback...");
            var created = context.Database.EnsureCreated();
            if (created)
            {
                logger.LogInformation("Database created via EnsureCreated() fallback.");
            }
            else
            {
                logger.LogWarning("EnsureCreated() did not create the database (it may already exist but be missing migrations).");
            }

            // Try applying pragmas even if EnsureCreated() didn't create the schema
            SqlitePragmaInitializer.ApplyPragmas(context);
            logger.LogInformation("SQLite pragmas applied successfully (fallback path)");
        }
        catch (Exception innerEx)
        {
            // Log full details. We intentionally do not rethrow to avoid breaking
            // test harnesses that may run with an alternate DbContext.
            logger.LogError(innerEx, "EnsureCreated() fallback also failed during database initialization");
        }
    }
}

// Initialize the SignalR sink now that the hub context is available
signalRSink.Initialize(app.Services.GetRequiredService<IHubContext<LogHub>>());

// Attempt to install Playwright browser binaries on startup (blocking with timeout).
// This reduces repeated missing-executable warnings during runtime by ensuring
// the browser artifacts are present before handling requests. If installation
// fails the app will continue to run; Playwright fallbacks will be skipped.
try
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Attempting Playwright browser install on startup (timeout: 90s)");

    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    var installTask = Task.Run(async () =>
    {
        // Local function to check if Playwright browsers are installed
        static bool ArePlaywrightBrowsersInstalled()
        {
            string playwrightPath;
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                playwrightPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                playwrightPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Caches", "ms-playwright");
            }
            else // Linux and others
            {
                playwrightPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "ms-playwright");
            }

            if (!Directory.Exists(playwrightPath)) return false;

            // Check for at least one browser directory (chromium-*, firefox-*, webkit-*)
            try
            {
                var browserDirs = Directory.GetDirectories(playwrightPath, "*-*");
                return browserDirs.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        try
        {
            // Check if Playwright browsers are already installed
            if (ArePlaywrightBrowsersInstalled())
            {
                logger.LogInformation("Playwright browsers are already installed, skipping startup installation");
                return true;
            }

            // Try reflection-based InstallAsync if available on the Playwright package
            try
            {
                var playwrightType = typeof(Microsoft.Playwright.Playwright);
                var installMethod = playwrightType.GetMethod("InstallAsync", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (installMethod != null)
                {
                    logger.LogInformation("Found Playwright.InstallAsync via reflection; invoking to install browsers...");
                    var installTaskObj = (System.Threading.Tasks.Task?)installMethod.Invoke(null, null);
                    if (installTaskObj != null)
                    {
                        await installTaskObj.ConfigureAwait(false);
                        logger.LogInformation("Playwright.InstallAsync completed successfully");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Reflection-based Playwright.InstallAsync attempt failed or not available");
            }

            // Fallback: try running the platform-specific Playwright install script (no Node.js required)
            try
            {
                // Use AppContext.BaseDirectory instead of Assembly.Location for single-file publish compatibility
                var assemblyDir = AppContext.BaseDirectory;
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    string? scriptPath = null;
                    string arguments;
                    if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    {
                        scriptPath = Path.Combine(assemblyDir, "playwright.ps1");
                        arguments = "install";
                        if (!File.Exists(scriptPath))
                        {
                            // Try in bin subfolder
                            scriptPath = Path.Combine(assemblyDir, "..", "..", "bin", "playwright.ps1");
                            if (!File.Exists(scriptPath))
                            {
                                scriptPath = null;
                            }
                        }
                        if (scriptPath != null)
                        {
                            logger.LogInformation("Running PowerShell Playwright install script: {Script}", scriptPath);
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "pwsh",
                                Arguments = $"\"{scriptPath}\" {arguments}",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            var processRunner = scope.ServiceProvider.GetService<Listenarr.Api.Services.IProcessRunner>();
                            if (processRunner != null)
                            {
                                var result = await processRunner.RunAsync(psi, timeoutMs: (int)TimeSpan.FromSeconds(90).TotalMilliseconds, cancellationToken: cts.Token).ConfigureAwait(false);
                                if (result.TimedOut)
                                {
                                    logger.LogWarning("PowerShell Playwright install script timed out after {Timeout}s", TimeSpan.FromSeconds(90).TotalSeconds);
                                }
                                else if (result.ExitCode == 0)
                                {
                                    logger.LogInformation("PowerShell Playwright install script completed successfully");
                                    return true;
                                }
                                else
                                {
                                    logger.LogWarning("PowerShell Playwright install script failed with exit code {ExitCode}. StdErr: {Err}", result.ExitCode, result.Stderr?.Length > 1000 ? result.Stderr.Substring(0, 1000) : result.Stderr);
                                }
                            }
                            else
                            {
                                logger.LogWarning("IProcessRunner is not available; skipping PowerShell Playwright install script fallback.");
                            }
                        }
                    }
                    else
                    {
                        scriptPath = Path.Combine(assemblyDir, "playwright.sh");
                        arguments = "install";
                        if (!File.Exists(scriptPath))
                        {
                            scriptPath = Path.Combine(assemblyDir, "..", "..", "bin", "playwright.sh");
                            if (!File.Exists(scriptPath))
                            {
                                scriptPath = null;
                            }
                        }
                        if (scriptPath != null)
                        {
                            logger.LogInformation("Running bash Playwright install script: {Script}", scriptPath);
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "bash",
                                Arguments = $"\"{scriptPath}\" {arguments}",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            var processRunner = scope.ServiceProvider.GetService<Listenarr.Api.Services.IProcessRunner>();
                            if (processRunner != null)
                            {
                                var result = await processRunner.RunAsync(psi, timeoutMs: (int)TimeSpan.FromSeconds(90).TotalMilliseconds, cancellationToken: cts.Token).ConfigureAwait(false);
                                if (result.TimedOut)
                                {
                                    logger.LogWarning("Bash Playwright install script timed out after {Timeout}s", TimeSpan.FromSeconds(90).TotalSeconds);
                                }
                                else if (result.ExitCode == 0)
                                {
                                    logger.LogInformation("Bash Playwright install script completed successfully");
                                    return true;
                                }
                                else
                                {
                                    logger.LogWarning("Bash Playwright install script failed with exit code {ExitCode}. StdErr: {Err}", result.ExitCode, result.Stderr?.Length > 1000 ? result.Stderr.Substring(0, 1000) : result.Stderr);
                                }
                            }
                            else
                            {
                                logger.LogWarning("IProcessRunner is not available; skipping bash Playwright install script fallback.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Platform-specific Playwright install script attempt failed");
            }

            // Fallback: try npx playwright install (requires Node.js available on PATH)
            try
            {
                // Install only Chromium to reduce download size and time. Give a much larger
                // timeout (10 minutes) because browser artifacts are large and may take time
                // on slow networks. Use the IProcessRunner when available so output is
                // captured and timeouts are enforced consistently.
                logger.LogInformation("Falling back to running 'npx playwright install chromium' to provision browsers (requires Node.js)");
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "npx",
                    Arguments = "playwright install chromium",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var processRunner = scope.ServiceProvider.GetService<Listenarr.Api.Services.IProcessRunner>();
                if (processRunner != null)
                {
                    var result = await processRunner.RunAsync(psi, timeoutMs: (int)TimeSpan.FromMinutes(10).TotalMilliseconds, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                    logger.LogDebug("Playwright npx output: {Out}\n{Err}", LogRedaction.RedactText(result.Stdout, LogRedaction.GetSensitiveValuesFromEnvironment()), LogRedaction.RedactText(result.Stderr, LogRedaction.GetSensitiveValuesFromEnvironment()));
                    if (result.TimedOut)
                    {
                        logger.LogWarning("'npx playwright install chromium' timed out after {Timeout} seconds", TimeSpan.FromMinutes(10).TotalSeconds);
                    }
                    else if (result.ExitCode == 0)
                    {
                        logger.LogInformation("'npx playwright install chromium' completed successfully");
                        return true;
                    }
                    else
                    {
                        logger.LogWarning("'npx playwright install chromium' did not complete successfully. ExitCode={ExitCode}", result.ExitCode);
                    }
                }
                else
                {
                    logger.LogWarning("IProcessRunner is not available; skipping 'npx playwright install chromium' fallback.");
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "'npx playwright install chromium' attempt failed or npx not available");
            }

            // As a last resort, ask the PlaywrightPageFetcher to ensure browsers are initialized.
            // Use the explicit TryEnsureInitializedAsync method so we can reliably detect whether
            // a browser instance is available instead of inferring success from a swallowed fetch.
            try
            {
                var pwFetcher = scope.ServiceProvider.GetService<Listenarr.Api.Services.IPlaywrightPageFetcher>();
                if (pwFetcher != null)
                {
                    logger.LogInformation("Invoking PlaywrightPageFetcher.TryEnsureInitializedAsync as final fallback");
                    var initialized = await pwFetcher.TryEnsureInitializedAsync(cts.Token).ConfigureAwait(false);
                    if (initialized)
                    {
                        logger.LogInformation("PlaywrightPageFetcher initialized browsers on fallback");
                        return true;
                    }
                    else
                    {
                        logger.LogWarning("PlaywrightPageFetcher fallback did not initialize browsers");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "PlaywrightPageFetcher fallback initialization failed");
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Playwright install attempt timed out");
            return false;
        }
    }, cts.Token);

    var finished = installTask.Wait(TimeSpan.FromSeconds(90));
    if (finished && installTask.IsCompletedSuccessfully && installTask.Result)
    {
        logger.LogInformation("Playwright installation succeeded on startup");
    }
    else
    {
        logger.LogWarning("Playwright installation did not complete successfully on startup. Playwright fallbacks will be skipped until browsers are installed.");
    }
}
catch (Exception ex)
{
    try { var l = app.Services.GetRequiredService<ILogger<Program>>(); l.LogWarning(ex, "Playwright installation attempt on startup failed"); } catch { }
}

// Ensure ffprobe is available on first launch (best-effort). Installation runs in background via
// the registered hosted service so the app can serve requests immediately.

// If the user passed --query-users, run the helper now that the DB is migrated and exit.
if (args is not null && args.Contains("--query-users"))
{
    QueryUsersProgram.Run();
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use forwarded headers middleware (must be early in pipeline)
// This processes X-Forwarded-For and X-Forwarded-Proto headers from the reverse proxy
app.UseForwardedHeaders();

// Note: HTTPS redirection is handled by the reverse proxy, not by this application

// Serve frontend static files from wwwroot (index.html + assets)
// DefaultFiles enables serving index.html when requesting '/'
app.UseDefaultFiles();
app.UseStaticFiles();

// Serve cached images from config/cache/images directory
var cacheImagesPath = Path.Combine(app.Environment.ContentRootPath, "config", "cache", "images");
if (Directory.Exists(cacheImagesPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(cacheImagesPath),
        RequestPath = "/config/cache/images"
    });
}

// Ensure routing middleware is enabled so endpoint routing features (CORS, Authorization)
// can be applied by subsequent middleware. This must run before UseCors()/UseAuthorization().
app.UseRouting();

// Enable CORS only in development (production should use reverse proxy for CORS)
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevOnly");
}
// Session-based authentication middleware
app.UseMiddleware<Listenarr.Api.Middleware.SessionAuthenticationMiddleware>();
// API key middleware: allows requests with a valid X-Api-Key or Authorization: ApiKey <key>
app.UseMiddleware<Listenarr.Api.Middleware.ApiKeyMiddleware>();
// Enforce authentication based on startup config
app.UseMiddleware<AuthenticationEnforcerMiddleware>();
// Validate antiforgery tokens for unsafe methods
app.UseMiddleware<Listenarr.Api.Middleware.AntiforgeryValidationMiddleware>();
app.UseAuthorization();

app.MapControllers();

// Map SignalR hub for real-time download updates
if (app.Environment.IsDevelopment())
{
    app.MapHub<DownloadHub>("/hubs/downloads").RequireCors("DevOnly");
    // Map SignalR hub for real-time log broadcasting
    app.MapHub<LogHub>("/hubs/logs").RequireCors("DevOnly");
    // Map SignalR hub for real-time settings updates
    app.MapHub<SettingsHub>("/hubs/settings").RequireCors("DevOnly");
}
else
{
    app.MapHub<DownloadHub>("/hubs/downloads");
    app.MapHub<LogHub>("/hubs/logs");
    // Map SignalR hub for real-time settings updates
    app.MapHub<SettingsHub>("/hubs/settings");
}

// SPA fallback: serve index.html for non-API routes so client-side routing works
app.MapFallbackToFile("index.html");

app.Run();
