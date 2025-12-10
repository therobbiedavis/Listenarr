// csharp
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Listenarr.Api.Services;

namespace Listenarr.Api.Tests
{
    /// <summary>
    /// Shared test fixture that centralizes common test registrations.
    /// Use with xUnit IClassFixture/Test collection fixtures to reduce duplication.
    /// </summary>
    public class TestServicesFixture : IDisposable
    {
        public ServiceProvider Provider { get; }
        public IServiceScopeFactory ScopeFactory { get; }

        public TestServicesFixture()
        {
            var services = new ServiceCollection();

            // Basic infra commonly used across tests
            services.AddLogging();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            services.AddSingleton<IConfiguration>(configuration);

            // Common cross-cutting services useful for IO-heavy tests
            services.AddSingleton<IFileStorage, Listenarr.Api.Services.FileStorage>();
            services.AddMemoryCache();
            services.AddSingleton<MetadataExtractionLimiter>();

            // Allow tests to override / add more registrations as needed by calling CreateScope + registering within scope
            Provider = services.BuildServiceProvider(validateScopes: true);
            ScopeFactory = Provider.GetRequiredService<IServiceScopeFactory>();
        }

        public void Dispose()
        {
            (Provider as IDisposable)?.Dispose();
        }
    }
}
