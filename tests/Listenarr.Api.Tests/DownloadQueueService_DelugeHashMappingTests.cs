using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class DownloadQueueService_DelugeHashMappingTests
    {
        [Fact]
        public async Task GetQueue_Maps_Deluge_Completed_By_TorrentHash_NoDuplicate()
        {
            var clientConfig = new DownloadClientConfiguration { Id = "deluge-1", Name = "Deluge", Type = "deluge", Host = "localhost", Port = 8112, IsEnabled = true };

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetDownloadClientConfigurationsAsync()).ReturnsAsync(new List<DownloadClientConfiguration> { clientConfig });
            configMock.Setup(c => c.GetDownloadClientConfigurationAsync(It.IsAny<string>())).ReturnsAsync(clientConfig);

            var gatewayMock = new Mock<IDownloadClientGateway>();

            var torrentHash = "1b272edc5997169e8aa8e5f302f1d0c2c5b1e2ee";
            var queueItem = new QueueItem { Id = torrentHash, Title = "Some title.m4b", Status = "completed", DownloadClientId = clientConfig.Id };

            gatewayMock
                .Setup(g => g.GetQueueAsync(It.IsAny<DownloadClientConfiguration>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<QueueItem> { queueItem });

            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings { ShowCompletedExternalDownloads = true });

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

            // Seed DB with a download that has TorrentHash metadata
            var dbFactory = provider.GetRequiredService<IDbContextFactory<ListenArrDbContext>>();
            string savedId;
            using (var db = dbFactory.CreateDbContext())
            {
                var download = new Download { Id = Guid.NewGuid().ToString(), Title = "Fourth Wing (2 of 2) by Rebecca Yarros [ENG / M4B]", DownloadClientId = clientConfig.Id, Status = DownloadStatus.Queued, Metadata = new System.Collections.Generic.Dictionary<string, object> { ["TorrentHash"] = torrentHash } };
                db.Downloads.Add(download);
                db.SaveChanges();
                savedId = download.Id;
            }

            var queueService = provider.GetRequiredService<IDownloadQueueService>();

            // Act
            var queue = await queueService.GetQueueAsync();

            // Assert: only one item and it is mapped to the DB download id
            Assert.Single(queue);
            Assert.Equal(savedId, queue[0].Id);
        }
    }
}