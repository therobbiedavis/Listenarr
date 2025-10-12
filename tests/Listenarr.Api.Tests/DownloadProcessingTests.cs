using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Moq;
using Listenarr.Api.Models;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;

namespace Listenarr.Api.Tests
{
    public class DownloadProcessingTests
    {
        [Fact]
        public async Task ProcessCompletedDownload_CreatesAudiobookFileAndBroadcasts()
        {
            // Arrange: in-memory DB
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);

            // Seed an audiobook and a download
            var book = new Audiobook { Title = "Test Book" };
            db.Audiobooks.Add(book);
            await db.SaveChangesAsync();

            var testPath = Path.Combine(Path.GetTempPath(), $"dl-test-{Guid.NewGuid()}.m4b");
            await File.WriteAllTextAsync(testPath, "dummy content");

            var download = new Download
            {
                Id = "dl-1",
                AudiobookId = book.Id,
                Title = "Test Book",
                Status = DownloadStatus.Downloading,
                DownloadPath = testPath,
                FinalPath = testPath,
                StartedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Mock metadata service
            var metadataMock = new Mock<IMetadataService>();
            metadataMock.Setup(m => m.ExtractFileMetadataAsync(It.IsAny<string>()))
                .ReturnsAsync(new AudioMetadata { Title = "Test Book", Duration = TimeSpan.FromSeconds(3600), Format = "m4b", Bitrate = 64000, SampleRate = 44100, Channels = 2 });

            // Mock hub context
            var hubClientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            hubClientsMock.Setup(h => h.All).Returns(clientProxyMock.Object);

            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            // Build service provider for scope factory and register metadata service
            var services = new ServiceCollection();
            services.AddSingleton<IMetadataService>(metadataMock.Object);
            services.AddSingleton(hubContextMock.Object);
            services.AddSingleton(db);
            // AudioFileService and dependencies (for creating AudiobookFile records)
            services.AddMemoryCache();
            services.AddSingleton<MetadataExtractionLimiter>();
            var loggerAfMock = new Mock<Microsoft.Extensions.Logging.ILogger<AudioFileService>>();
            services.AddScoped<IAudioFileService, Listenarr.Api.Services.AudioFileService>();
            services.AddSingleton(loggerAfMock.Object);
            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            // Construct DownloadService with required dependencies (use nulls/mocks where not needed)
            var repoMock = new Mock<IAudiobookRepository>();
            var configMock = new Mock<IConfigurationService>();
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<DownloadService>>();
            var httpClient = new System.Net.Http.HttpClient();
            var pathMappingMock = new Mock<IRemotePathMappingService>();
            var searchMock = new Mock<ISearchService>();

            var downloadService = new DownloadService(
                repoMock.Object,
                configMock.Object,
                db,
                loggerMock.Object,
                httpClient,
                scopeFactory,
                pathMappingMock.Object,
                searchMock.Object,
                hubContextMock.Object);

            // Act
            await downloadService.ProcessCompletedDownloadAsync(download.Id, download.FinalPath);

            // Assert: audiobook file created
            var file = await db.AudiobookFiles.FirstOrDefaultAsync(f => f.AudiobookId == book.Id);
            Assert.NotNull(file);
            Assert.Equal(download.FinalPath, file.Path);
            Assert.NotNull(file.DurationSeconds);
            Assert.InRange(file.DurationSeconds.Value, 3599.0, 3601.0);
            Assert.Equal("m4b", file.Format);

            // Assert: broadcast called
            clientProxyMock.Verify(c => c.SendCoreAsync("DownloadUpdate", It.IsAny<object[]>(), default), Times.AtLeastOnce);
        }
    }
}
