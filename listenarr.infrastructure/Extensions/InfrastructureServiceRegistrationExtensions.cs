// csharp
using Microsoft.Extensions.DependencyInjection;
using Listenarr.Api.Services;

namespace Listenarr.Infrastructure.Extensions
{
    /// <summary>
    /// Registers infrastructure implementations (repositories, persistence adapters, etc.).
    /// Keep this in the Infrastructure project so Program.cs can call a single registration surface.
    /// </summary>
    public static class InfrastructureServiceRegistrationExtensions
    {
        public static IServiceCollection AddListenarrInfrastructure(this IServiceCollection services)
        {
            // Register repository implementations (moved from API into Infrastructure)
            services.AddScoped<IAudiobookRepository, AudiobookRepository>();

            // TODO: add other infrastructure registrations (e.g. IFileStore, IUnitOfWork) here
            return services;
        }
    }
}
