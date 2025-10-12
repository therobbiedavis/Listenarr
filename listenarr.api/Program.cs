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
using Microsoft.AspNetCore.Authentication.Cookies;
using Listenarr.Api.Middleware;
using Microsoft.EntityFrameworkCore;

// Check for special CLI helpers before building the web host
// Pass a non-null args array to satisfy nullable analysis
Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:5656");
var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

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

// Add default HTTP client for other services
builder.Services.AddHttpClient();

// Register our custom services
// Compute an absolute path for the SQLite file based on the content root so
// the published exe will create/use the intended config/database path even
// when the working directory differs.
var sqliteDbPath = Path.Combine(builder.Environment.ContentRootPath, "config", "database", "listenarr.db");
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

// Typed HttpClients with automatic decompression for scraping services
builder.Services.AddHttpClient<IAmazonSearchService, AmazonSearchService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

builder.Services.AddHttpClient<IAudibleSearchService, AudibleSearchService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    });

// Register Playwright page fetcher for JS-rendered pages and bot-workarounds
// Playwright-based page fetcher removed; AudibleMetadataService will create Playwright on-demand if needed.

// Add CORS support for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5173",
                    "https://localhost:5173",
                    "http://localhost:3000",
                    "https://localhost:3000"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // Required for SignalR
        });

    // Development fallback (use cautiously). This can help diagnose unexpected origin mismatches.
    options.AddPolicy("DevAll", p => p
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod());
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Authentication: Cookie-based (minimal default). This will be enforced by middleware when configured.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/api/account/login";
        options.Cookie.Name = "listenarr_auth";
        // Harden cookie settings
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
            // Require HTTPS in production, but allow insecure cookies during local development
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.None
                : CookieSecurePolicy.Always; // require HTTPS in production
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
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
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    });
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
        // Apply any pending migrations
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

app.UseHttpsRedirection();

    // Serve frontend static files from wwwroot (index.html + assets)
    // DefaultFiles enables serving index.html when requesting '/'
    app.UseDefaultFiles();
    app.UseStaticFiles();

// Enable CORS (use restrictive policy by default, allow override via environment var)
var corsPolicy = Environment.GetEnvironmentVariable("LISTENARR_CORS_POLICY");
if (!string.IsNullOrWhiteSpace(corsPolicy) && corsPolicy == "DevAll")
{
    app.UseCors("DevAll");
}
else
{
    app.UseCors("AllowVueApp");
}
    // Enable authentication middleware
    app.UseAuthentication();
    // API key middleware: allows requests with a valid X-Api-Key or Authorization: ApiKey <key>
    app.UseMiddleware<Listenarr.Api.Middleware.ApiKeyMiddleware>();
    // Enforce authentication based on startup config
    app.UseMiddleware<AuthenticationEnforcerMiddleware>();
    // Validate antiforgery tokens for unsafe methods
    app.UseMiddleware<Listenarr.Api.Middleware.AntiforgeryValidationMiddleware>();
    app.UseAuthorization();
app.UseAuthorization();

app.MapControllers();

// Map SignalR hub for real-time download updates
app.MapHub<DownloadHub>("/hubs/downloads");

    // SPA fallback: serve index.html for non-API routes so client-side routing works
    app.MapFallbackToFile("index.html");

app.Run();
