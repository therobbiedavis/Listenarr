using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Tests
{
    /// <summary>
    /// Lightweight test service factory to build an IServiceProvider for unit tests.
    /// Call BuildServiceProvider and pass additional registrations via the configure callback.
    /// This keeps tests using DI instead of directly new-ing large services.
    /// </summary>
    public static class TestServiceFactory
    {
        /// <summary>
        /// Build a ServiceProvider for tests.
        /// - Registers Logging and an empty IConfiguration by default.
        /// - Allows the caller to add/override registrations via the configure callback.
        /// </summary>
        public static ServiceProvider BuildServiceProvider(Action<IServiceCollection>? configure = null)
        {
            var services = new ServiceCollection();

            // Basic infrastructure commonly used in tests
            services.AddLogging();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            services.AddSingleton<IConfiguration>(configuration);

            // Let the test add or override services (mocks, fakes, concrete implementations)
            configure?.Invoke(services);

            return services.BuildServiceProvider(validateScopes: true);
        }

        /// <summary>
        /// Convenience accessor.
        /// </summary>
        public static T GetRequiredService<T>(ServiceProvider provider) where T : notnull
            => provider.GetRequiredService<T>();
    }
}
