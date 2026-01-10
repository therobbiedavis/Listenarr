using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class DownloadQueueService_PurgeRetriesTests
    {
        [Fact]
        public async Task PurgeRetriesAndKeepsIfAppearsOnRetry()
        {
            var clientConfig = new DownloadClientConfiguration { Id = "c1", Name = "Deluge", Type = "deluge", Host = "localhost", Port = 8112, IsEnabled = true };

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetDownloadClientConfigurationsAsync()).ReturnsAsync(new List<DownloadClientConfiguration> { clientConfig });
            configMock.Setup(c => c.GetDownloadClientConfigurationAsync(It.IsAny<string>())).ReturnsAsync(clientConfig);

            // client gateway: first call return empty queue, second call return queue with matching torrent
            var gatewayMock = new Mock<IDownloadClientGateway>();
            var callCount = 0;
            gatewayMock.Setup(g => g.GetQueueAsync(It.IsAny<DownloadClientConfiguration>(), It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1) return new List<QueueItem>();
                return new List<QueueItem> { new QueueItem { Id = "foundId", Title = "Test Title", Size = 100 } };
            });

            var provider = TestServiceFactory.BuildServiceProvider(services =>
            {
                services.AddSingleton<IConfigurationService>(configMock.Object);
                services.AddSingleton<IDownloadClientGateway>(gatewayMock.Object);
                // Register the real DownloadQueueService (override test implementation)
                services.AddSingleton<IDownloadQueueService, Listenarr.Api.Services.DownloadQueueService>();
                // Use an in-memory DB
                services.AddDbContextFactory<ListenArrDbContext>(options => options.UseInMemoryDatabase($"test-db-{Guid.NewGuid()}"));
                // Ensure memory cache is available
                services.AddSingleton<Microsoft.Extensions.Caching.Memory.IMemoryCache>(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
                // Minimal mapping service mock
                services.AddSingleton<Listenarr.Api.Services.IRemotePathMappingService>(Mock.Of<Listenarr.Api.Services.IRemotePathMappingService>());
                // Provide an IHttpClientFactory for the DownloadQueueService constructor
                services.AddSingleton<System.Net.Http.IHttpClientFactory>(Mock.Of<System.Net.Http.IHttpClientFactory>());
            });

            // Seed DB with a download that would be considered orphaned initially
            var dbFactory = provider.GetRequiredService<IDbContextFactory<ListenArrDbContext>>();
            using (var db = await dbFactory.CreateDbContextAsync())
            {
                db.Downloads.Add(new Download { Id = "d1", Title = "Test Title", DownloadClientId = "c1", Status = DownloadStatus.Queued, TotalSize = 100 });
                await db.SaveChangesAsync();
            }

            var queueService = provider.GetRequiredService<IDownloadQueueService>();

            // Act
            var queue = await queueService.GetQueueAsync();

            // Assert: the DB download should not be purged because it appeared on retry
            using (var db = await dbFactory.CreateDbContextAsync())
            {
                var downloads = await db.Downloads.ToListAsync();
                Assert.Single(downloads);
                Assert.Equal("d1", downloads[0].Id);
            }

            // Ensure GetQueueAsync was called at least twice (retry occurred)
            gatewayMock.Verify(g => g.GetQueueAsync(It.IsAny<DownloadClientConfiguration>(), It.IsAny<System.Threading.CancellationToken>()), Times.AtLeast(2));
        }

        [Fact]
        public async Task PurgeDeferredForRecentAdd()
        {
            var clientConfig = new DownloadClientConfiguration { Id = "c1", Name = "Deluge", Type = "deluge", Host = "localhost", Port = 8112, IsEnabled = true };

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetDownloadClientConfigurationsAsync()).ReturnsAsync(new List<DownloadClientConfiguration> { clientConfig });
            configMock.Setup(c => c.GetDownloadClientConfigurationAsync(It.IsAny<string>())).ReturnsAsync(clientConfig);

            // client gateway: always returns empty queue
            var gatewayMock = new Mock<IDownloadClientGateway>();
            gatewayMock.Setup(g => g.GetQueueAsync(It.IsAny<DownloadClientConfiguration>(), It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(new List<QueueItem>());

            var provider = TestServiceFactory.BuildServiceProvider(services =>
            {
                services.AddSingleton<IConfigurationService>(configMock.Object);
                services.AddSingleton<IDownloadClientGateway>(gatewayMock.Object);
                services.AddSingleton<IDownloadQueueService, Listenarr.Api.Services.DownloadQueueService>();
                services.AddDbContextFactory<ListenArrDbContext>(options => options.UseInMemoryDatabase($"test-db-{Guid.NewGuid()}"));
                services.AddSingleton<Microsoft.Extensions.Caching.Memory.IMemoryCache>(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
                services.AddSingleton<Listenarr.Api.Services.IRemotePathMappingService>(Mock.Of<Listenarr.Api.Services.IRemotePathMappingService>());
                services.AddSingleton<System.Net.Http.IHttpClientFactory>(Mock.Of<System.Net.Http.IHttpClientFactory>());
            });

            // Seed DB with a *recent* download that would otherwise be considered orphaned
            var dbFactory = provider.GetRequiredService<IDbContextFactory<ListenArrDbContext>>();
            using (var db = await dbFactory.CreateDbContextAsync())
            {
                db.Downloads.Add(new Download { Id = "d2", Title = "Recent Item", DownloadClientId = "c1", Status = DownloadStatus.Queued, TotalSize = 100, StartedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }

            var queueService = provider.GetRequiredService<IDownloadQueueService>();

            // Act
            var queue = await queueService.GetQueueAsync();

            // Assert: the DB download should not be purged because it's within the recent threshold
            using (var db = await dbFactory.CreateDbContextAsync())
            {
                var downloads = await db.Downloads.ToListAsync();
                Assert.Single(downloads);
                Assert.Equal("d2", downloads[0].Id);
            }
        }
    }
}
