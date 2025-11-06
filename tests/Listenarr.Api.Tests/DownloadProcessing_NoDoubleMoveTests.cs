using System;
using System.IO;
using System.Linq;
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
    public class DownloadProcessing_NoDoubleMoveTests
    {
        [Fact]
        public async Task CompletedDownload_LinkedToAudiobook_DoesNotMoveToUnknownAuthor()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);

            // Create audiobook with author
            var book = new Audiobook { Title = "Pride and Prejudice", Authors = new System.Collections.Generic.List<string> { "Jane Austen" } };
            db.Audiobooks.Add(book);
            await db.SaveChangesAsync();

            // Create a temp directory for the expected output (simulate configured OutputPath)
            var outputRoot = Path.Combine(Path.GetTempPath(), "listenarr-test-output", Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputRoot);

            // Create the expected subdirectory structure that the naming pattern will generate
            var authorDir = Path.Combine(outputRoot, "Jane Austen");
            var seriesDir = Path.Combine(authorDir, "Pride and Prejudice");
            Directory.CreateDirectory(seriesDir);

            // Create source file (as if downloader put it here)
            var sourceFile = Path.Combine(Path.GetTempPath(), $"dl-dbl-{Guid.NewGuid()}.mp3");
            await File.WriteAllTextAsync(sourceFile, "dummy");

            // Create download record linked to audiobook
            var download = new Download
            {
                Id = "dbl-1",
                AudiobookId = book.Id,
                Title = book.Title,
                Status = DownloadStatus.Completed,
                DownloadPath = sourceFile,
                FinalPath = sourceFile,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Mock services
            var metadataMock = new Mock<IMetadataService>();
            metadataMock.Setup(m => m.ExtractFileMetadataAsync(It.IsAny<string>()))
                .ReturnsAsync(new AudioMetadata { Title = "", Artist = "", Format = "mp3" });

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings { OutputPath = outputRoot, EnableMetadataProcessing = true, CompletedFileAction = "Move" });

            var hubClientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            hubClientsMock.Setup(h => h.All).Returns(clientProxyMock.Object);
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            // Compose DI for scopeFactory used by DownloadService
            var services = new ServiceCollection();
            services.AddSingleton<IMetadataService>(metadataMock.Object);
            services.AddSingleton<IConfigurationService>(configMock.Object);
            services.AddSingleton(db);
            services.AddMemoryCache();
            services.AddSingleton<MetadataExtractionLimiter>();
            services.AddSingleton(hubContextMock.Object);
            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var repoMock = new Mock<IAudiobookRepository>();
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<DownloadService>>();
            var httpClient = new System.Net.Http.HttpClient();
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            var cacheMock = new Mock<IMemoryCache>();
            var dbFactoryMock = new Mock<IDbContextFactory<ListenArrDbContext>>();
            dbFactoryMock.Setup(f => f.CreateDbContext()).Returns(db);
            var pathMappingMock = new Mock<IRemotePathMappingService>();
            var searchMock = new Mock<ISearchService>();

            var downloadService = new DownloadService(
                repoMock.Object,
                configMock.Object,
                dbFactoryMock.Object,
                loggerMock.Object,
                httpClient,
                httpClientFactoryMock.Object,
                scopeFactory,
                pathMappingMock.Object,
                searchMock.Object,
                hubContextMock.Object,
                cacheMock.Object);

            // Act - process completed download
            await downloadService.ProcessCompletedDownloadAsync(download.Id, download.FinalPath);

            // Assert: only one AudiobookFile exists for this audiobook and its path contains the audiobook author directory
            var files = await db.AudiobookFiles.Where(f => f.AudiobookId == book.Id).ToListAsync();
            Assert.Single(files);
            var createdPath = files[0].Path ?? string.Empty;
            var norm = createdPath.ToLowerInvariant();
            // Expect the author as a single folder name (with space preserved)
            Assert.Contains("jane austen", norm);

            // Also assert there's no AudiobookFile under an "unknown author" path
            var unknownFiles = await db.AudiobookFiles.Where(f => f.Path.ToLowerInvariant().Contains("unknown author")).ToListAsync();
            Assert.Empty(unknownFiles);

            // Cleanup
            try { Directory.Delete(outputRoot, true); } catch { }
            try { File.Delete(sourceFile); } catch { }
        }
    }
}
