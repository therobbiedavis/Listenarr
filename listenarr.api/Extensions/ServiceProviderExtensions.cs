using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Listenarr.Api.Extensions
{
    /// <summary>
    /// Helper extensions to resolve a ListenArrDbContext in a way that prefers <c>IDbContextFactory&lt;ListenArrDbContext&gt;</c>
    /// but falls back to an <c>IServiceProvider</c>-registered <c>ListenArrDbContext</c> for tests/legacy code.
    /// This simplifies migrating call sites that currently use <c>GetRequiredService&lt;ListenArrDbContext&gt;</c>.
    /// </summary>
    public static class ServiceProviderExtensions
    {
        /// <summary>
        /// Resolve a <c>ListenArrDbContext</c> from the provider.
        /// - If an <c>IDbContextFactory&lt;ListenArrDbContext&gt;</c> is registered, creates a new context via <c>CreateDbContextAsync</c>.
        /// - Otherwise falls back to resolving a <c>ListenArrDbContext</c> directly from the provider.
        /// Callers are responsible for disposing the returned context when appropriate.
        /// </summary>
        public static async Task<ListenArrDbContext> GetListenArrDbContextAsync(this IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            // Resolve both registrations if present.
            var directContext = provider.GetService<ListenArrDbContext>();
            var factory = provider.GetService<IDbContextFactory<ListenArrDbContext>>();

            // If only a factory is registered, create a new context (safe for callers to dispose).
            if (factory != null && directContext == null)
            {
                return await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            }

            // If a direct ListenArrDbContext is registered (tests/legacy), prefer it to avoid returning an
            // instance that might be the same shared/singleton object. Returning the registered instance
            // prevents callers from disposing a shared instance when they call Dispose()/using.
            if (directContext != null)
            {
                return directContext;
            }

            // As a last resort, resolve the required service (will throw if not available).
            return provider.GetRequiredService<ListenArrDbContext>();
        }

        /// <summary>
        /// Synchronous helper for code paths that cannot be async.
        /// Prefer async method when possible.
        /// </summary>
        public static ListenArrDbContext GetListenArrDbContext(this IServiceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            // Resolve both registrations if present.
            var directContext = provider.GetService<ListenArrDbContext>();
            var factory = provider.GetService<IDbContextFactory<ListenArrDbContext>>();

            // If only a factory is registered, create a new context (safe for callers to dispose).
            if (factory != null && directContext == null)
            {
                // IDbContextFactory has a synchronous CreateDbContext overload.
                return factory.CreateDbContext();
            }

            // If a direct ListenArrDbContext is registered (tests/legacy), prefer it to avoid returning an
            // instance that might be the same shared/singleton object. Returning the registered instance
            // prevents callers from disposing a shared instance when they call Dispose()/using.
            if (directContext != null)
            {
                return directContext;
            }

            // As a last resort, resolve the required service (will throw if not available).
            return provider.GetRequiredService<ListenArrDbContext>();
        }
    }
}
