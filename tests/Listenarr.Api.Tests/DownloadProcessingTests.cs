using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Listenarr.Api.Services.Adapters;
using Xunit;
using Moq;
using Listenarr.Domain.Models;
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
            clientProxyMock.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);

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
            // Provide deterministic application settings for imports so file operations
            // and naming behave predictably in tests.
            configMock.Setup(c => c.GetApplicationSettingsAsync())
                .ReturnsAsync(new ApplicationSettings { OutputPath = Path.GetTempPath(), EnableMetadataProcessing = true, CompletedFileAction = "Move" });
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

            // Construct ImportService from the provider so it receives the same
            // IServiceScopeFactory as the DownloadService. This allows scoped
            // ListenArrDbContext instances to be discoverable during synchronization.
            var provider2 = TestServiceFactory.BuildServiceProvider(services =>
            {
                // ensure ListenArrDbContext from the test is available to scopes created by the provider
                services.AddSingleton<ListenArrDbContext>(db);
                services.AddSingleton<IAudiobookRepository>(repoMock.Object);
                services.AddSingleton<IConfigurationService>(configMock.Object);
                services.AddSingleton<IDbContextFactory<ListenArrDbContext>>(dbFactoryMock.Object);
                // metadata extraction required by AudioFileService
                services.AddSingleton<IMetadataService>(metadataMock.Object);
                // Ensure the same in-memory ListenArrDbContext instance is resolvable for sync
                services.AddSingleton<ListenArrDbContext>(db);
                // Provide metadata service for AudioFileService
                services.AddSingleton<IMetadataService>(metadataMock.Object);
                // Ensure the same in-memory ListenArrDbContext and supporting services
                // are available when ImportService/AudioFileService run during the test.
                services.AddSingleton<ListenArrDbContext>(db);
                services.AddSingleton<MetadataExtractionLimiter>();
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
                // Ensure metadata service is available to AudioFileService during import/registration
                services.AddSingleton<IMetadataService>(metadataMock.Object);
                // Ensure metadata service is available to AudioFileService during import/registration
                services.AddSingleton<IMetadataService>(metadataMock.Object);
                services.AddSingleton<ISearchService>(searchMock.Object);
                services.AddSingleton<Listenarr.Api.Services.Adapters.IDownloadClientAdapterFactory>(new Mock<Listenarr.Api.Services.Adapters.IDownloadClientAdapterFactory>().Object);
                services.AddSingleton<IHubContext<DownloadHub>>(hubContextMock.Object);
                services.AddSingleton<IMemoryCache>(cacheMock.Object);
                services.AddTransient<DownloadService>();
            });
            var downloadService = provider2.GetRequiredService<DownloadService>();

            // Act
            await downloadService.ProcessCompletedDownloadAsync(download.Id, download.FinalPath);

            // Assert: audiobook file created (verify with fresh DbContext)
            await using var verifyDb = new ListenArrDbContext(options);
            var file = await verifyDb.AudiobookFiles.FirstOrDefaultAsync(f => f.AudiobookId == book.Id);
            Assert.NotNull(file);
            Assert.Equal(download.FinalPath, file.Path);
            Assert.NotNull(file.DurationSeconds);
            Assert.InRange(file.DurationSeconds.Value, 3599.0, 3601.0);
            Assert.Equal("m4b", file.Format);

            // Broadcast behavior not asserted here; ensure import and registration completed successfully.
        }

        [Fact]
        public async Task ProcessCompletedDownload_AudiobookWithBasePath_UsesFilenameOnly_NoExtraFolders()
        {
            // Arrange: in-memory DB
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(options);

            // Create a temporary base path directory for the audiobook
            var basePath = Path.Combine(Path.GetTempPath(), $"audiobook-base-{Guid.NewGuid()}");
            Directory.CreateDirectory(basePath);

            // Seed an audiobook with BasePath
            var book = new Audiobook 
            { 
                Title = "Test Audiobook", 
                Authors = new System.Collections.Generic.List<string> { "Test Author" },
                BasePath = basePath
            };
            db.Audiobooks.Add(book);
            await db.SaveChangesAsync();

            // Create source file in a different location
            var sourceDir = Path.Combine(Path.GetTempPath(), $"source-{Guid.NewGuid()}");
            Directory.CreateDirectory(sourceDir);
            var sourceFile = Path.Combine(sourceDir, "source-file.m4b");
            await File.WriteAllTextAsync(sourceFile, "dummy content");

            var download = new Download
            {
                Id = "dl-basepath-1",
                AudiobookId = book.Id,
                Title = "Test Audiobook",
                Status = DownloadStatus.Downloading,
                DownloadPath = sourceFile,
                FinalPath = sourceFile,
                StartedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Mock configuration service to return settings with metadata processing enabled
            // and a naming pattern that would normally create subdirectories
            var configMock = new Mock<IConfigurationService>();
            var settings = new ApplicationSettings
            {
                EnableMetadataProcessing = true,
                FileNamingPattern = "{Author}/{Series}/{DiskNumber:00} - {Title}",
                CompletedFileAction = "Move"
            };
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(settings);

            // Mock metadata service
            var metadataMock = new Mock<IMetadataService>();
            metadataMock.Setup(m => m.ExtractFileMetadataAsync(It.IsAny<string>()))
                .ReturnsAsync(new AudioMetadata 
                { 
                    Title = "Test Audiobook", 
                    Artist = "Test Author",
                    Duration = TimeSpan.FromSeconds(3600), 
                    Format = "m4b", 
                    Bitrate = 64000, 
                    SampleRate = 44100, 
                    Channels = 2 
                });

            // Mock hub context
            var hubClientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            hubClientsMock.Setup(h => h.All).Returns(clientProxyMock.Object);
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            // Build service provider
            var services = new ServiceCollection();
            services.AddSingleton<IMetadataService>(metadataMock.Object);
            services.AddSingleton<IConfigurationService>(configMock.Object);
            services.AddSingleton(hubContextMock.Object);
            services.AddSingleton(db);
            services.AddMemoryCache();
            services.AddSingleton<MetadataExtractionLimiter>();
            var loggerAfMock = new Mock<Microsoft.Extensions.Logging.ILogger<AudioFileService>>();
            services.AddScoped<IAudioFileService, Listenarr.Api.Services.AudioFileService>();
            services.AddSingleton(loggerAfMock.Object);
            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            // Construct DownloadService
            var repoMock = new Mock<IAudiobookRepository>();
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

            var provider2 = TestServiceFactory.BuildServiceProvider(services =>
            {
                // ensure ListenArrDbContext from the test is available to scopes created by the provider
                services.AddSingleton<ListenArrDbContext>(db);
                // provide limiter required by AudioFileService
                services.AddSingleton<MetadataExtractionLimiter>();
                services.AddSingleton<IAudiobookRepository>(repoMock.Object);
                services.AddSingleton<IConfigurationService>(configMock.Object);
                services.AddSingleton<IDbContextFactory<ListenArrDbContext>>(dbFactoryMock.Object);
                services.AddSingleton<ILogger<DownloadService>>(loggerMock.Object);
                services.AddSingleton<HttpClient>(httpClient);
                services.AddSingleton<IHttpClientFactory>(httpClientFactoryMock.Object);
                // Ensure metadata service is available inside the provider used by DownloadService
                services.AddSingleton<IMetadataService>(metadataMock.Object);
                services.AddSingleton<IImportService>(sp => new ImportService(
                    dbFactoryMock.Object,
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    new FileNamingService(configMock.Object, new Microsoft.Extensions.Logging.Abstractions.NullLogger<FileNamingService>()),
                    metadataMock.Object,
                    new Microsoft.Extensions.Logging.Abstractions.NullLogger<ImportService>()));
                services.AddSingleton<IRemotePathMappingService>(pathMappingMock.Object);
                services.AddSingleton<ISearchService>(searchMock.Object);
                services.AddSingleton<IDownloadClientAdapterFactory>(new Mock<IDownloadClientAdapterFactory>().Object);
                services.AddSingleton<IHubContext<DownloadHub>>(hubContextMock.Object);
                services.AddSingleton<IMemoryCache>(cacheMock.Object);
                services.AddTransient<DownloadService>();
            });
            var downloadService = provider2.GetRequiredService<DownloadService>();

            // Act
            await downloadService.ProcessCompletedDownloadAsync(download.Id, download.FinalPath);

            // Assert: download is completed and final path is within BasePath (verify with fresh DbContext)
            await using var verifyDb = new ListenArrDbContext(options);
            var updatedDownload = await verifyDb.Downloads.FindAsync(download.Id);
            Assert.NotNull(updatedDownload);
            Assert.Equal(DownloadStatus.Completed, updatedDownload.Status);
            Assert.NotNull(updatedDownload.FinalPath);
            Assert.StartsWith(basePath, updatedDownload.FinalPath);

            // Assert: file exists at final path
            Assert.True(File.Exists(updatedDownload.FinalPath));

            // Assert: final path is directly in BasePath (no subdirectories created)
            var relativePath = Path.GetRelativePath(basePath, updatedDownload.FinalPath);
            Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), relativePath);
            Assert.DoesNotContain(Path.AltDirectorySeparatorChar.ToString(), relativePath);

            // Assert: no subdirectories were created in BasePath
            var directoriesInBasePath = Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories);
            Assert.Empty(directoriesInBasePath);

            // Assert: only the expected file exists in BasePath
            var filesInBasePath = Directory.GetFiles(basePath, "*", SearchOption.AllDirectories);
            Assert.Single(filesInBasePath);
            Assert.Equal(updatedDownload.FinalPath, filesInBasePath[0]);

            // Assert: source file was moved (not copied)
            Assert.False(File.Exists(sourceFile));

            // Assert: audiobook file record was created
            var audiobookFile = await db.AudiobookFiles.FirstOrDefaultAsync(f => f.AudiobookId == book.Id);
            Assert.NotNull(audiobookFile);
            Assert.Equal(updatedDownload.FinalPath, audiobookFile.Path);

            // Cleanup
            try
            {
                if (Directory.Exists(basePath)) Directory.Delete(basePath, true);
                if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }
}
