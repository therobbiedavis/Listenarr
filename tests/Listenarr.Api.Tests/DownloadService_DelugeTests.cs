using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;

namespace Listenarr.Api.Tests
{
    public class DownloadService_DelugeTests
    {
        [Fact]
        public async Task StartDownloadAsync_Uses_Deluge_Client_When_Enabled()
        {
            var delugeConfig = new DownloadClientConfiguration { Id = "deluge-1", Name = "Deluge", Type = "deluge", IsEnabled = true };

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetDownloadClientConfigurationsAsync()).ReturnsAsync(new List<DownloadClientConfiguration> { delugeConfig });
            configMock.Setup(c => c.GetDownloadClientConfigurationAsync(It.IsAny<string>())).ReturnsAsync(delugeConfig);
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings());

            var gatewayMock = new Mock<IDownloadClientGateway>();
            gatewayMock
                .Setup(g => g.AddAsync(It.Is<DownloadClientConfiguration>(d => d.Id == delugeConfig.Id), It.IsAny<SearchResult>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("abc123")
                .Verifiable();
            gatewayMock
                .Setup(g => g.GetQueueAsync(It.IsAny<DownloadClientConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new System.Collections.Generic.List<QueueItem>());


            var provider = TestServiceFactory.BuildServiceProvider(services =>
            {
                services.AddSingleton<IConfigurationService>(configMock.Object);
                services.AddSingleton<IDownloadClientGateway>(gatewayMock.Object);
                // Provide minimal missing services as mocks
                services.AddSingleton(Mock.Of<IAudiobookRepository>());
                services.AddDbContextFactory<ListenArrDbContext>(options => options.UseInMemoryDatabase($"test-db-{System.Guid.NewGuid()}"));
                services.AddSingleton(Mock.Of<IHttpClientFactory>());
                services.AddSingleton(Mock.Of<IRemotePathMappingService>());
                services.AddSingleton(Mock.Of<IImportService>());
                services.AddSingleton(Mock.Of<ISearchService>());
                services.AddSingleton(Mock.Of<IMemoryCache>());
                services.AddSingleton(Mock.Of<ICompletedDownloadProcessor>());
                services.AddSingleton(Mock.Of<IAppMetricsService>());
                services.AddSingleton(Mock.Of<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>());
            });

            var downloadService = new DownloadService(
                provider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>(),
                provider.GetRequiredService<IAudiobookRepository>(),
                provider.GetRequiredService<IConfigurationService>(),
                provider.GetRequiredService<IDbContextFactory<ListenArrDbContext>>(),
                provider.GetRequiredService<ILogger<DownloadService>>(),
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider.GetRequiredService<IRemotePathMappingService>(),
                provider.GetRequiredService<IImportService>(),
                provider.GetRequiredService<ISearchService>(),
                provider.GetRequiredService<IDownloadClientGateway>(),
                provider.GetRequiredService<IMemoryCache>(),
                provider.GetRequiredService<IDownloadQueueService>(),
                provider.GetRequiredService<ICompletedDownloadProcessor>(),
                provider.GetRequiredService<IAppMetricsService>(),
                provider.GetRequiredService<NotificationService>());

            var searchResult = new SearchResult { Title = "Test", MagnetLink = "magnet:?xt=urn:btih:abc", TorrentUrl = null, Size = 1234 };

            var downloadId = await downloadService.StartDownloadAsync(searchResult, delugeConfig.Id, null);

            // Verify AddAsync was invoked on the gateway with the Deluge configuration
            gatewayMock.Verify();
            Assert.NotNull(downloadId);
        }

        [Fact]
        public async Task StartDownloadAsync_AutoSelects_Deluge_When_No_QBittorrent()
        {
            var delugeConfig = new DownloadClientConfiguration { Id = "deluge-1", Name = "Deluge", Type = "deluge", IsEnabled = true };

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetDownloadClientConfigurationsAsync()).ReturnsAsync(new List<DownloadClientConfiguration> { delugeConfig });
            configMock.Setup(c => c.GetDownloadClientConfigurationAsync(It.IsAny<string>())).ReturnsAsync(delugeConfig);
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings());

            var gatewayMock = new Mock<IDownloadClientGateway>();
            gatewayMock
                .Setup(g => g.AddAsync(It.Is<DownloadClientConfiguration>(d => d.Id == delugeConfig.Id), It.IsAny<SearchResult>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("abc123")
                .Verifiable();
            gatewayMock
                .Setup(g => g.GetQueueAsync(It.IsAny<DownloadClientConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new System.Collections.Generic.List<QueueItem>());

            var provider = TestServiceFactory.BuildServiceProvider(services =>
            {
                services.AddSingleton<IConfigurationService>(configMock.Object);
                services.AddSingleton<IDownloadClientGateway>(gatewayMock.Object);
                // Provide minimal missing services as mocks
                services.AddSingleton(Mock.Of<IAudiobookRepository>());
                services.AddDbContextFactory<ListenArrDbContext>(options => options.UseInMemoryDatabase($"test-db-{System.Guid.NewGuid()}"));
                services.AddSingleton(Mock.Of<IHttpClientFactory>());
                services.AddSingleton(Mock.Of<IRemotePathMappingService>());
                services.AddSingleton(Mock.Of<IImportService>());
                services.AddSingleton(Mock.Of<ISearchService>());
                services.AddSingleton(Mock.Of<IMemoryCache>());
                services.AddSingleton(Mock.Of<ICompletedDownloadProcessor>());
                services.AddSingleton(Mock.Of<IAppMetricsService>());
                services.AddSingleton(Mock.Of<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>());
            });

            var downloadService = new DownloadService(
                provider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>(),
                provider.GetRequiredService<IAudiobookRepository>(),
                provider.GetRequiredService<IConfigurationService>(),
                provider.GetRequiredService<IDbContextFactory<ListenArrDbContext>>(),
                provider.GetRequiredService<ILogger<DownloadService>>(),
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider.GetRequiredService<IRemotePathMappingService>(),
                provider.GetRequiredService<IImportService>(),
                provider.GetRequiredService<ISearchService>(),
                provider.GetRequiredService<IDownloadClientGateway>(),
                provider.GetRequiredService<IMemoryCache>(),
                provider.GetRequiredService<IDownloadQueueService>(),
                provider.GetRequiredService<ICompletedDownloadProcessor>(),
                provider.GetRequiredService<IAppMetricsService>(),
                provider.GetRequiredService<NotificationService>());

            var searchResult = new SearchResult { Title = "Test", MagnetLink = "magnet:?xt=urn:btih:abc", TorrentUrl = null, Size = 1234 };

            var downloadId = await downloadService.StartDownloadAsync(searchResult, null, null);

            // Verify AddAsync was invoked on the gateway with the Deluge configuration
            gatewayMock.Verify();
            Assert.NotNull(downloadId);
        }

        [Fact]
        public async Task GetQueue_Maps_Deluge_QueueItem_By_TorrentHash()
        {
            var delugeConfig = new DownloadClientConfiguration { Id = "deluge-1", Name = "Deluge", Type = "deluge", IsEnabled = true };

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetDownloadClientConfigurationsAsync()).ReturnsAsync(new List<DownloadClientConfiguration> { delugeConfig });
            configMock.Setup(c => c.GetDownloadClientConfigurationAsync(It.IsAny<string>())).ReturnsAsync(delugeConfig);
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings());

            var gatewayMock = new Mock<IDownloadClientGateway>();

            // Queue item with different title but matching torrent hash
            var torrentHash = "1b272edc5997169e8aa8e5f302f1d0c2c5b1e2ee";
            var queueItem = new QueueItem { Id = torrentHash, Title = "Some other title.m4b", DownloadClientId = delugeConfig.Id };

            gatewayMock
                .Setup(g => g.GetQueueAsync(It.IsAny<DownloadClientConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new System.Collections.Generic.List<QueueItem> { queueItem });

            var provider = TestServiceFactory.BuildServiceProvider(services =>
            {
                services.AddSingleton<IConfigurationService>(configMock.Object);
                services.AddSingleton<IDownloadClientGateway>(gatewayMock.Object);
                // Provide minimal missing services as mocks
                services.AddSingleton(Mock.Of<IAudiobookRepository>());
                services.AddDbContextFactory<ListenArrDbContext>(options => options.UseInMemoryDatabase($"test-db-{System.Guid.NewGuid()}"));
                services.AddSingleton(Mock.Of<IHttpClientFactory>());
                services.AddSingleton(Mock.Of<IRemotePathMappingService>());
                services.AddSingleton(Mock.Of<IImportService>());
                services.AddSingleton(Mock.Of<ISearchService>());
                services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
                services.AddSingleton(Mock.Of<ICompletedDownloadProcessor>());
                services.AddSingleton(Mock.Of<IAppMetricsService>());
                services.AddSingleton(Mock.Of<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>());
            });

            // Insert a Listenarr DB download with the TorrentHash metadata set
            var dbFactory = provider.GetRequiredService<IDbContextFactory<ListenArrDbContext>>();
            string savedId;
            using (var ctx = dbFactory.CreateDbContext())
            {
                var download = new Download
                {
                    Id = System.Guid.NewGuid().ToString(),
                    Title = "Fourth Wing (2 of 2) by Rebecca Yarros [ENG / M4B]",
                    DownloadClientId = delugeConfig.Id,
                    Status = DownloadStatus.Queued,
                    Metadata = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["TorrentHash"] = torrentHash
                    }
                };
                ctx.Downloads.Add(download);
                ctx.SaveChanges();
                savedId = download.Id;
            }

            var downloadService = new DownloadService(
                provider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>(),
                provider.GetRequiredService<IAudiobookRepository>(),
                provider.GetRequiredService<IConfigurationService>(),
                provider.GetRequiredService<IDbContextFactory<ListenArrDbContext>>(),
                provider.GetRequiredService<ILogger<DownloadService>>(),
                provider.GetRequiredService<IHttpClientFactory>(),
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider.GetRequiredService<IRemotePathMappingService>(),
                provider.GetRequiredService<IImportService>(),
                provider.GetRequiredService<ISearchService>(),
                provider.GetRequiredService<IDownloadClientGateway>(),
                provider.GetRequiredService<IMemoryCache>(),
                provider.GetRequiredService<IDownloadQueueService>(),
                provider.GetRequiredService<ICompletedDownloadProcessor>(),
                provider.GetRequiredService<IAppMetricsService>(),
                provider.GetRequiredService<NotificationService>());

            var result = await downloadService.GetQueueAsync();

            // Expect that the queue item is mapped to the Listenarr DB download ID
            var mapped = result.FirstOrDefault(q => q.DownloadClientId == delugeConfig.Id && q.Id == savedId.ToString());
            Assert.NotNull(mapped);
        }
    }
}
