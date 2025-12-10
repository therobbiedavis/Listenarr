using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Listenarr.Api.Extensions;
using Listenarr.Api.Services;

namespace Listenarr.Api.Tests
{
    public class HostedServicesRegistrationTests
    {
        [Fact]
        public void AddListenarrHostedServices_RegistersHostedServicesAndSingletons()
        {
            // Arrange
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

            // Act
            services.AddListenarrHostedServices(config);

            // Assert - hosted services registered
            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ScanBackgroundService));
            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(DownloadProcessingChannelConsumer));
            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(MoveBackgroundService));
            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ImageCacheCleanupService));
            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(TempFileCleanupService));
            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(DownloadMonitorService));
            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(QueueMonitorService));
            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(AutomaticSearchService));
            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(FfmpegInstallBackgroundService));
            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(MetadataRescanService));
            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(DownloadProcessingBackgroundService));

            // Assert - singletons / supporting services registered
            Assert.Contains(services, d => d.ServiceType == typeof(IScanQueueService) && d.Lifetime == ServiceLifetime.Singleton);
            Assert.Contains(services, d => d.ServiceType == typeof(DownloadProcessingChannel) && d.Lifetime == ServiceLifetime.Singleton);
            Assert.Contains(services, d => d.ServiceType == typeof(IMoveQueueService) && d.Lifetime == ServiceLifetime.Singleton);
        }
    }
}
