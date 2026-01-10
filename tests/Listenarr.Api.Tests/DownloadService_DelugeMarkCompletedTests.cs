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
    public class DownloadService_DelugeMarkCompletedTests
    {
        [Fact]
        public async Task GetQueue_Marks_DB_Download_Completed_When_Deluge_Report_Completed()
        {
            var clientConfig = new DownloadClientConfiguration { Id = "deluge-1", Name = "Deluge", Type = "deluge", Host = "localhost", Port = 8112, IsEnabled = true };

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetDownloadClientConfigurationsAsync()).ReturnsAsync(new List<DownloadClientConfiguration> { clientConfig });
            configMock.Setup(c => c.GetDownloadClientConfigurationAsync(It.IsAny<string>())).ReturnsAsync(clientConfig);
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings { ShowCompletedExternalDownloads = true });

            var gatewayMock = new Mock<IDownloadClientGateway>();

            var torrentHash = "1b272edc5997169e8aa8e5f302f1d0c2c5b1e2ee";
            var queueItem = new QueueItem { Id = torrentHash, Title = "Some title.m4b", Status = "completed", DownloadClientId = clientConfig.Id };

            gatewayMock
                .Setup(g => g.GetQueueAsync(It.IsAny<DownloadClientConfiguration>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new List<QueueItem> { queueItem });

            var completedProcessorMock = new Mock<ICompletedDownloadProcessor>();

            var provider = TestServiceFactory.BuildServiceProvider(services =>
            {
                services.AddSingleton<IConfigurationService>(configMock.Object);
                services.AddSingleton<IDownloadClientGateway>(gatewayMock.Object);
                services.AddSingleton<ICompletedDownloadProcessor>(completedProcessorMock.Object);
                services.AddSingleton<IDownloadService, Listenarr.Api.Services.DownloadService>();
                services.AddDbContextFactory<ListenArrDbContext>(options => options.UseInMemoryDatabase($"test-db-{Guid.NewGuid()}"));
                services.AddSingleton<Microsoft.Extensions.Caching.Memory.IMemoryCache>(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
                services.AddSingleton<Listenarr.Api.Services.IRemotePathMappingService>(Mock.Of<Listenarr.Api.Services.IRemotePathMappingService>());
                services.AddSingleton<System.Net.Http.IHttpClientFactory>(Mock.Of<System.Net.Http.IHttpClientFactory>());
                services.AddSingleton<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>(Mock.Of<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>());
                // Provide minimal missing services as mocks
                services.AddSingleton(Mock.Of<Listenarr.Api.Services.IAudiobookRepository>());
                services.AddSingleton(Mock.Of<IImportService>());
                services.AddSingleton(Mock.Of<ISearchService>());
                services.AddSingleton(Mock.Of<IAppMetricsService>());
                services.AddSingleton<NotificationService>(new NotificationService(new System.Net.Http.HttpClient(), Mock.Of<Microsoft.Extensions.Logging.ILogger<NotificationService>>(), configMock.Object, Mock.Of<Listenarr.Api.Services.INotificationPayloadBuilder>()));
                services.AddSingleton(Mock.Of<IDownloadQueueService>());
            });


            // Seed DB with a download that has TorrentHash metadata and is still Queued
            var dbFactory = provider.GetRequiredService<IDbContextFactory<ListenArrDbContext>>();
            string savedId;
            using (var db = dbFactory.CreateDbContext())
            {
                var download = new Download { Id = Guid.NewGuid().ToString(), Title = "Fourth Wing (2 of 2)", DownloadClientId = clientConfig.Id, Status = DownloadStatus.Queued, TotalSize = 12345, Metadata = new System.Collections.Generic.Dictionary<string, object> { ["TorrentHash"] = torrentHash } };
                db.Downloads.Add(download);
                db.SaveChanges();
                savedId = download.Id;
            }

            var downloadService = provider.GetRequiredService<IDownloadService>();

            // Act
            var queue = await downloadService.GetQueueAsync();

            // Assert: DB download should have been marked as Completed
            using (var db2 = dbFactory.CreateDbContext())
            {
                var d = await db2.Downloads.FindAsync(savedId);
                Assert.NotNull(d);
                Assert.Equal(DownloadStatus.Completed, d.Status);
                Assert.Equal(100M, d.Progress);
            }

            // Verify CompletedDownloadProcessor was invoked for this download (background trigger)
            var completedProcessor = provider.GetRequiredService<ICompletedDownloadProcessor>();
            var mockProcessor = Mock.Get(completedProcessor);
            mockProcessor.Verify(p => p.ProcessCompletedDownloadAsync(It.Is<string>(s => s == savedId), It.IsAny<string>()), Times.AtLeastOnce);
        }
    }
}
