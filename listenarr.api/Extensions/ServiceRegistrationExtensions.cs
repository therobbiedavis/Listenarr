 // csharp
 using System;
 using System.Linq;
 using System.Net;
 using System.Net.Http;
 using Microsoft.Extensions.Configuration;
 using Microsoft.Extensions.DependencyInjection;
 using Polly;
 using Polly.Extensions.Http;
 using Microsoft.EntityFrameworkCore;
 using Listenarr.Infrastructure.Models;
 using Listenarr.Api.Services.Adapters;
 using Listenarr.Api.Services;
 using Microsoft.Extensions.Options;

namespace Listenarr.Api.Extensions
{
    /// <summary>
    /// Helper extension methods to split Program.cs registrations into focused modules.
    /// This file centralizes common HttpClient registrations so callers (Program.cs / tests)
    /// can reuse a single consistent registration surface.
    /// </summary>
    public static partial class ServiceRegistrationExtensions
    {
        /// <summary>
        /// Registers common HttpClients (named/typed) and Polly policies used by the app.
        /// This centralizes policy creation so Program.cs doesn't duplicate logic.
        /// Adds named clients for download adapters and typed clients used by services.
        /// </summary>
        public static IServiceCollection AddListenarrHttpClients(this IServiceCollection services, IConfiguration config)
        {
            // Shared retry/circuit-breaker policy builders
            var retryPolicy = HttpPolicyExtensions.HandleTransientHttpError()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        // Logging can be done inside services that consume HttpClient via ILogger
                    });

            var circuitBreakerPolicy = HttpPolicyExtensions.HandleTransientHttpError()
                .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30));

            // Default HTTP client
            services.AddHttpClient();

            // Generic named client used by legacy code paths for downloads
            services.AddHttpClient("DownloadClient")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.All,
                    UseCookies = false
                })
                .AddPolicyHandler(retryPolicy)
                .AddPolicyHandler(circuitBreakerPolicy);

            // Per-adapter named clients (qbittorrent, transmission, sabnzbd, nzbget)
            services.AddHttpClient("qbittorrent")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.All,
                    UseCookies = false
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddPolicyHandler(circuitBreakerPolicy)
                .AddPolicyHandler(retryPolicy);

            services.AddHttpClient("transmission")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.All,
                    UseCookies = false
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddPolicyHandler(circuitBreakerPolicy)
                .AddPolicyHandler(retryPolicy);

            services.AddHttpClient("sabnzbd")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.All,
                    UseCookies = false
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddPolicyHandler(circuitBreakerPolicy)
                .AddPolicyHandler(retryPolicy);

            services.AddHttpClient("nzbget")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.All,
                    UseCookies = false
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddPolicyHandler(circuitBreakerPolicy)
                .AddPolicyHandler(retryPolicy);

            // Direct download client with extended timeout for large files
            services.AddHttpClient("DirectDownload")
                .ConfigureHttpClient(client =>
                {
                    client.Timeout = TimeSpan.FromHours(2);
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.All
                })
                .AddPolicyHandler(HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .Or<TaskCanceledException>()
                    .CircuitBreakerAsync(3, TimeSpan.FromMinutes(1)))
                .AddPolicyHandler(HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .Or<TaskCanceledException>()
                    .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

            // US-origin client (supports optional proxy via configuration)
            services.AddHttpClient("us")
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    var handler = new HttpClientHandler
                    {
                        AutomaticDecompression = DecompressionMethods.All
                    };

                    try
                    {
                        var section = config.GetSection("ExternalRequests");
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
                    catch
                    {
                        // Swallow here; caller services can detect proxy misconfigurations via failing requests.
                    }

                    return handler;
                });

            // Typed clients used by scraping/search services. Add consistent handlers + policies.
            services.AddHttpClient<Listenarr.Api.Services.IAmazonSearchService, Listenarr.Api.Services.AmazonSearchService>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
                .AddPolicyHandler(HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .OrResult((HttpResponseMessage r) => r.StatusCode == HttpStatusCode.Forbidden
                                                         || r.StatusCode == (HttpStatusCode)429
                                                         || r.StatusCode == HttpStatusCode.ServiceUnavailable)
                    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
                .AddPolicyHandler(HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .OrResult((HttpResponseMessage r) => r.StatusCode == HttpStatusCode.Forbidden
                                                         || r.StatusCode == (HttpStatusCode)429
                                                         || r.StatusCode == HttpStatusCode.ServiceUnavailable)
                    .CircuitBreakerAsync(6, TimeSpan.FromMinutes(2)));

            services.AddHttpClient<Listenarr.Api.Services.IAudibleSearchService, Listenarr.Api.Services.AudibleSearchService>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
                .AddPolicyHandler(circuitBreakerPolicy)
                .AddPolicyHandler(retryPolicy);

            // Misc scraping/metadata clients (Audible metadata, Audimeta, Audnexus)
            services.AddHttpClient<Listenarr.Api.Services.IAudibleMetadataService, Listenarr.Api.Services.AudibleMetadataService>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
                .AddPolicyHandler(circuitBreakerPolicy)
                .AddPolicyHandler(retryPolicy);

            services.AddHttpClient<Listenarr.Api.Services.AudimetaService>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
                .AddPolicyHandler(circuitBreakerPolicy)
                .AddPolicyHandler(retryPolicy);

            services.AddHttpClient<Listenarr.Api.Services.AudnexusService>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
                .AddPolicyHandler(circuitBreakerPolicy)
                .AddPolicyHandler(retryPolicy);

            return services;
        }

        /// <summary>
        /// Register persistence (DbContextFactory + compatibility DbContext).
        /// This is intentionally minimal and safe for test hosts.
        /// </summary>
        public static IServiceCollection AddListenarrPersistence(this IServiceCollection services, IConfiguration configuration, string sqliteDbPath)
        {
            // Use DbContextFactory for hosted services; also register a scoped DbContext for controllers.
            services.AddDbContextFactory<ListenArrDbContext>(options =>
            {
                options.UseSqlite($"Data Source={sqliteDbPath}", sqliteOptions =>
                {
                    // Keep migrations assembly pointing to the Infrastructure project so
                    // migrations authored there are discovered and applied at runtime.
                    sqliteOptions.MigrationsAssembly(typeof(Listenarr.Infrastructure.Repositories.QualityProfileRepository).Assembly.GetName().Name);
                });
            });

            services.AddDbContext<ListenArrDbContext>(options =>
            {
                options.UseSqlite($"Data Source={sqliteDbPath}", sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly(typeof(Listenarr.Infrastructure.Repositories.QualityProfileRepository).Assembly.GetName().Name);
                });
            });

            // Register infrastructure repository implementations
            services.AddScoped<Listenarr.Application.Repositories.IQualityProfileRepository, Listenarr.Infrastructure.Repositories.QualityProfileRepository>();

            return services;
        }

        /// <summary>
        /// Registers adapters, their options and validators.
        /// </summary>
        public static IServiceCollection AddListenarrAdapters(this IServiceCollection services, IConfiguration config)
        {
            // Bind download client definitions from configuration and expose via IOptions
            services.Configure<DownloadClientsOptions>(config.GetSection("DownloadClients"));

            // Validate download client configuration at startup, surface errors early
            services.AddSingleton<IValidateOptions<DownloadClientsOptions>, DownloadClientsOptionsValidator>();

            // Register default adapter implementations (can be extended). Use concrete types from Adapters namespace.
            services.AddScoped<IDownloadClientAdapter, Listenarr.Api.Services.Adapters.QbittorrentAdapter>();

            // Title matching service extracted from DownloadService for easier testing
            services.AddScoped<ITitleMatchingService, Listenarr.Api.Services.Adapters.TitleMatchingService>();

            // Register available adapter implementations. Keep adapters scoped because they may depend on scoped services.
            services.AddScoped<IDownloadClientAdapter, Listenarr.Api.Services.Adapters.QbittorrentAdapter>();
            services.AddScoped<IDownloadClientAdapter, Listenarr.Api.Services.Adapters.SabnzbdAdapter>();
            
            // Register the concrete factory as scoped so it can safely resolve scoped adapters via DI.
            services.AddScoped<IDownloadClientAdapterFactory, Listenarr.Api.Services.Adapters.DownloadClientAdapterFactory>();

            // Register notification payload builder adapter for DI so callers can inject/mokc payload construction.
            services.AddSingleton<INotificationPayloadBuilder, NotificationPayloadBuilderAdapter>();

            // File storage abstraction used throughout services to isolate System.IO for testing
            services.AddSingleton<IFileStorage, Listenarr.Api.Services.FileStorage>();

            // SignalR broadcaster abstraction used to centralize broadcast logic and simplify testing
            services.AddSingleton<Listenarr.Application.Services.IHubBroadcaster, Listenarr.Api.Services.SignalRHubBroadcaster>();

            return services;
        }
    }
}
