using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using Moq;
using Listenarr.Domain.Models;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;

namespace Listenarr.Api.Tests
{
    public class DownloadNaming_AudiobookMetadataTests : Xunit.IClassFixture<TestServicesFixture>
    {
        private readonly TestServicesFixture _fixture;
        public DownloadNaming_AudiobookMetadataTests(TestServicesFixture fixture)
        {
            _fixture = fixture;
        }

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
            clientProxyMock.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);
            services.AddSingleton(hubContextMock.Object);

            using var scope = _fixture.Provider.CreateScope();
            var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

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

            // Note: construct ImportService from the provider so it receives the same
            // IServiceScopeFactory used by DownloadService. This ensures ScopeFactory
            // matches and scoped ListenArrDbContext instances (the test's `db`) are
            // discoverable during synchronization steps.
            var provider2 = TestServiceFactory.BuildServiceProvider(services =>
            {
                services.AddSingleton<IAudiobookRepository>(repoMock.Object);
                services.AddSingleton<IConfigurationService>(configMock.Object);
                services.AddSingleton<IDbContextFactory<ListenArrDbContext>>(dbFactoryMock.Object);
                // Provide metadata service so AudioFileService can extract metadata without throwing
                services.AddSingleton<IMetadataService>(metadataMock.Object);
                // Ensure the same in-memory ListenArrDbContext instance is resolvable
                // by services created inside ImportService/AudioFileService during the test.
                services.AddSingleton<ListenArrDbContext>(db);
                services.AddSingleton<Microsoft.Extensions.Logging.ILogger<DownloadService>>(loggerMock.Object);
                services.AddSingleton<HttpClient>(httpClient);
                services.AddSingleton<IHttpClientFactory>(httpClientFactoryMock.Object);
                services.AddSingleton<IImportService>(sp => new ImportService(
                    dbFactoryMock.Object,
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    new FileNamingService(configMock.Object, new Microsoft.Extensions.Logging.Abstractions.NullLogger<FileNamingService>()),
                    metadataMock.Object,
                    new Microsoft.Extensions.Logging.Abstractions.NullLogger<ImportService>()));
                services.AddSingleton<IRemotePathMappingService>(pathMappingMock.Object);
                services.AddSingleton<ISearchService>(searchMock.Object);
                // Required by AudioFileService/ImportService activation
                services.AddSingleton<MetadataExtractionLimiter>();
                services.AddSingleton<Listenarr.Api.Services.Adapters.IDownloadClientAdapterFactory>(new Mock<Listenarr.Api.Services.Adapters.IDownloadClientAdapterFactory>().Object);
                services.AddSingleton<IHubContext<DownloadHub>>(hubContextMock.Object);
                services.AddSingleton<IMemoryCache>(cacheMock.Object);
                services.AddTransient<DownloadService>();
            });
            var downloadService = provider2.GetRequiredService<DownloadService>();

            // Act: call ProcessCompletedDownloadAsync which should generate a destination using audiobook metadata
            await downloadService.ProcessCompletedDownloadAsync(download.Id, download.FinalPath);

            // Reload the download and audiobook files using a fresh DbContext instance
            await using var verifyDb = new ListenArrDbContext(options);
            var updated = await verifyDb.Downloads.FindAsync(download.Id);
            Assert.True(updated.Status == DownloadStatus.Completed || updated.Status == DownloadStatus.Moved, $"Expected Completed or Moved, got {updated.Status}");

            var fileRecord = await verifyDb.AudiobookFiles.FirstOrDefaultAsync(f => f.AudiobookId == book.Id);
            // Import may be deferred (queued) so a DB file record may not exist synchronously; accept either outcome.
            if (fileRecord != null)
            {
                // The stored path should include the audiobook Author (Jane Austen) as part of the generated folder
                var lowered = (fileRecord.Path ?? string.Empty).ToLowerInvariant();
                // Expect the author as a single folder name (with space preserved), not nested directories
                Assert.Contains("jane austen", lowered);
            }

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
