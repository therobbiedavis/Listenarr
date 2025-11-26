using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using Moq;
using Listenarr.Api.Models;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;

namespace Listenarr.Api.Tests
{
    public class DownloadNaming_AudiobookMetadataTests
    {
        [Fact]
        public async Task ProcessCompletedDownload_UsesAudiobookMetadata_ForNaming()
        {
            // Setup in-memory DB
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);

            // Create audiobook with Authors so naming should pick them
            var book = new Audiobook { Title = "Pride and Prejudice", Authors = new System.Collections.Generic.List<string> { "Jane Austen" } };
            db.Audiobooks.Add(book);
            await db.SaveChangesAsync();

            // Create a temporary source file
            var testFile = Path.Combine(Path.GetTempPath(), $"dl-naming-{Guid.NewGuid()}.mp3");
            await File.WriteAllTextAsync(testFile, "dummy content");

            var download = new Download
            {
                Id = "dln-1",
                AudiobookId = book.Id,
                Title = book.Title,
                Status = DownloadStatus.Completed,
                DownloadPath = testFile,
                FinalPath = testFile,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Create the output directory that the code will use as fallback
            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "completed");
            Directory.CreateDirectory(outputDir);

            // Create the expected subdirectory structure
            var authorDir = Path.Combine(outputDir, "Jane Austen");
            var seriesDir = Path.Combine(authorDir, "Pride and Prejudice");
            Directory.CreateDirectory(seriesDir);

            // Mock metadata service to return minimal metadata (so audiobook metadata must be used)
            var metadataMock = new Mock<IMetadataService>();
            metadataMock.Setup(m => m.ExtractFileMetadataAsync(It.IsAny<string>()))
                .ReturnsAsync(new AudioMetadata { Title = "", Artist = "", Format = "mp3" });

            // Setup required DI services for scope factory used inside DownloadService
            var services = new ServiceCollection();
            services.AddSingleton<IMetadataService>(metadataMock.Object);
            services.AddSingleton(db);
            services.AddMemoryCache();
            services.AddSingleton<MetadataExtractionLimiter>();

            // Minimal hub mocks
            var hubClientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            hubClientsMock.Setup(h => h.All).Returns(clientProxyMock.Object);
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);
            services.AddSingleton(hubContextMock.Object);

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            // Create DownloadService
            var repoMock = new Mock<IAudiobookRepository>();
            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings { OutputPath = outputDir, EnableMetadataProcessing = true, CompletedFileAction = "Move" });
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<DownloadService>>();
            var httpClient = new System.Net.Http.HttpClient();
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            var cacheMock = new Mock<IMemoryCache>();
            var dbFactoryMock = new Mock<IDbContextFactory<ListenArrDbContext>>();
            dbFactoryMock.Setup(f => f.CreateDbContext()).Returns(() => new ListenArrDbContext(options));
            dbFactoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(() => new ListenArrDbContext(options));
            var pathMappingMock = new Mock<IRemotePathMappingService>();
            var searchMock = new Mock<ISearchService>();

            var importService = new ImportService(dbFactoryMock.Object, scopeFactory, new FileNamingService(configMock.Object, new Microsoft.Extensions.Logging.Abstractions.NullLogger<FileNamingService>()), metadataMock.Object, new Microsoft.Extensions.Logging.Abstractions.NullLogger<ImportService>());

            var downloadService = new DownloadService(
                repoMock.Object,
                configMock.Object,
                dbFactoryMock.Object,
                loggerMock.Object,
                httpClient,
                httpClientFactoryMock.Object,
                scopeFactory,
                importService,
                pathMappingMock.Object,
                searchMock.Object,
                hubContextMock.Object,
                cacheMock.Object,
                null); // NotificationService is optional

            // Act: call ProcessCompletedDownloadAsync which should generate a destination using audiobook metadata
            await downloadService.ProcessCompletedDownloadAsync(download.Id, download.FinalPath);

            // Reload the download and audiobook files
            var updated = await db.Downloads.FindAsync(download.Id);
            Assert.Equal(DownloadStatus.Completed, updated.Status);

            var fileRecord = await db.AudiobookFiles.FirstOrDefaultAsync(f => f.AudiobookId == book.Id);
            Assert.NotNull(fileRecord);

            // The stored path should include the audiobook Author (Jane Austen) as part of the generated folder
            var lowered = (fileRecord.Path ?? string.Empty).ToLowerInvariant();
            // Expect the author as a single folder name (with space preserved), not nested directories
            Assert.Contains("jane austen", lowered);

            // Cleanup
            try
            {
                if (File.Exists(testFile)) File.Delete(testFile);
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }
}
