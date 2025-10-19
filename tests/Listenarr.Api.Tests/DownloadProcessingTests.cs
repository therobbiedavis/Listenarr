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

            // Assert: download is completed and final path is within BasePath
            var updatedDownload = await db.Downloads.FindAsync(download.Id);
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
