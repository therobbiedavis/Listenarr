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
using Listenarr.Api.Models;
using Listenarr.Api.Hubs;
using Listenarr.Api.Middleware;
using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Serilog;
using Polly;
using Polly.Extensions.Http;

// Check for special CLI helpers before building the web host
// Pass a non-null args array to satisfy nullable analysis
var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());

// Configure Serilog for file logging with rotation and SignalR broadcasting
var logFilePath = Path.Combine(builder.Environment.ContentRootPath, "config", "logs", "listenarr-.log");
var signalRSink = new SignalRLogSink();
Log.Logger = new Serilog.LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        logFilePath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30, // Keep 30 days of logs
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Sink(signalRSink) // Add SignalR sink for real-time log broadcasting
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

// Add HTTP client for Audimeta service
builder.Services.AddHttpClient<AudimetaService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

// Add HTTP client for Audnexus service
builder.Services.AddHttpClient<AudnexusService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

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
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, duration) =>
            {
                Console.WriteLine($"[CIRCUIT BREAKER] Download client circuit opened due to {outcome.Exception?.Message ?? "policy trigger"}. Breaking for {duration.TotalSeconds}s");
            },
            onReset: () =>
            {
                Console.WriteLine("[CIRCUIT BREAKER] Download client circuit reset - service recovered");
            }
        ))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Console.WriteLine($"[RETRY] Download client retry attempt {retryAttempt} after {timespan.TotalSeconds}s delay");
            }
        ));

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
                Console.WriteLine($"[CIRCUIT BREAKER] Direct download circuit opened. Breaking for {duration.TotalMinutes}m");
            },
            onReset: () =>
            {
                Console.WriteLine("[CIRCUIT BREAKER] Direct download circuit reset");
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
var sqliteDbPath = Path.Combine(builder.Environment.ContentRootPath, "config", "database", "listenarr.db");

// Use DbContextFactory for better scoped context management
builder.Services.AddDbContextFactory<ListenArrDbContext>(options =>
    options.UseSqlite($"Data Source={sqliteDbPath}"));

// Keep AddDbContext for services that still inject DbContext directly (AudiobookRepository, etc.)
builder.Services.AddDbContext<ListenArrDbContext>(options =>
    options.UseSqlite($"Data Source={sqliteDbPath}"));

builder.Services.AddScoped<IAudiobookRepository, AudiobookRepository>();
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();
// Startup config: read config.json (optional) and expose via IStartupConfigService
builder.Services.AddSingleton<IStartupConfigService, StartupConfigService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IMetadataService, MetadataService>();
builder.Services.AddScoped<IAudioFileService, AudioFileService>();
// Metadata extraction limiter to bound concurrent ffprobe calls
builder.Services.AddSingleton<MetadataExtractionLimiter>();
// Ffmpeg installer: provides a bundled ffprobe binary when not present on the system
builder.Services.AddSingleton<IFfmpegService, FfmpegInstallerService>();
// Service to accept client-pushed download updates and maintain recent-push cache
builder.Services.AddSingleton<Listenarr.Api.Services.DownloadPushService>();
builder.Services.AddScoped<IAmazonAsinService, AmazonAsinService>();
builder.Services.AddScoped<IDownloadService, DownloadService>();
// NOTE: IAudibleMetadataService is already registered as a typed HttpClient above.
// Removing duplicate scoped registration to avoid overriding the typed client configuration.
builder.Services.AddScoped<IOpenLibraryService, OpenLibraryService>();
builder.Services.AddScoped<IImageCacheService, ImageCacheService>();
builder.Services.AddScoped<IFileNamingService, FileNamingService>();
builder.Services.AddScoped<IRemotePathMappingService, RemotePathMappingService>();
builder.Services.AddScoped<ISystemService, SystemService>();
builder.Services.AddScoped<IQualityProfileService, QualityProfileService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<ILoginRateLimiter, LoginRateLimiter>();
builder.Services.AddScoped<IDownloadProcessingQueueService, DownloadProcessingQueueService>();

// Toast service for broadcasting UI toasts via SignalR
builder.Services.AddSingleton<IToastService, ToastService>();

// Notification service for webhook notifications
builder.Services.AddScoped<NotificationService>();

// Allow services to access the current HttpContext so NotificationService can
// build absolute image URLs when the startup config doesn't supply a base URL.
builder.Services.AddHttpContextAccessor();

// Always register session service, but it will check config internally
builder.Services.AddScoped<ISessionService, ConditionalSessionService>();

// Scan queue: enqueue folder scans to be processed in the background
builder.Services.AddSingleton<IScanQueueService, ScanQueueService>();
// Background worker to consume scan jobs and persist audiobook files
builder.Services.AddHostedService<ScanBackgroundService>();

// Register background service for daily cache cleanup
builder.Services.AddHostedService<ImageCacheCleanupService>();

// Register background service for temp file cleanup
builder.Services.AddHostedService<TempFileCleanupService>();

// Register background service for download monitoring and real-time updates
builder.Services.AddHostedService<DownloadMonitorService>();

// Register background service for queue monitoring (external clients) and real-time updates
builder.Services.AddHostedService<QueueMonitorService>();

// Register background service for automatic audiobook searching
builder.Services.AddHostedService<AutomaticSearchService>();
// Background installer for ffprobe - run in background so startup isn't blocked
builder.Services.AddHostedService<FfmpegInstallBackgroundService>();

// Background service to rescan files missing metadata
builder.Services.AddHostedService<MetadataRescanService>();

// Register background service for download processing queue
builder.Services.AddHostedService<DownloadProcessingBackgroundService>();

// Typed HttpClients with automatic decompression for scraping services
builder.Services.AddHttpClient<IAmazonSearchService, AmazonSearchService>()
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
                Console.WriteLine($"[CIRCUIT BREAKER] Amazon search circuit opened. Breaking for {duration.TotalMinutes}m");
            },
            onReset: () =>
            {
                Console.WriteLine("[CIRCUIT BREAKER] Amazon search circuit reset");
            }
        ))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(1)));

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
                Console.WriteLine($"[CIRCUIT BREAKER] Audible search circuit opened. Breaking for {duration.TotalMinutes}m");
            },
            onReset: () =>
            {
                Console.WriteLine("[CIRCUIT BREAKER] Audible search circuit reset");
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
        Console.WriteLine($"[WARNING] Failed to configure proxy settings: {ex.Message}");
    }

    return handler;
});

// Register Playwright page fetcher for JS-rendered pages and bot-workarounds
// Playwright-based page fetcher removed; AudibleMetadataService will create Playwright on-demand if needed.

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
        Console.WriteLine($"[WARNING] Failed to include XML comments in Swagger: {ex.Message}");
    }
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
        logger.LogInformation("Applying database migrations...");
        // Apply any pending migrations (including AddDefaultMetadataSources which adds Audimeta and Audnexus)
        context.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully");
        
        // Apply SQLite PRAGMA settings after database is created
        SqlitePragmaInitializer.ApplyPragmas(context);
        logger.LogInformation("SQLite pragmas applied successfully");
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

// Initialize the SignalR sink now that the service provider is available
signalRSink.Initialize(app.Services);

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
}
else
{
    app.MapHub<DownloadHub>("/hubs/downloads");
    app.MapHub<LogHub>("/hubs/logs");
}

    // SPA fallback: serve index.html for non-API routes so client-side routing works
    app.MapFallbackToFile("index.html");

app.Run();
