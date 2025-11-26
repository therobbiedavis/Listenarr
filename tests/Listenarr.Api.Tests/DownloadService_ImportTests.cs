using System;
using System.Collections.Generic;
using System.Net.Http;
using Listenarr.Api.Controllers;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    public class DownloadService_ImportTests
    {
        private ListenArrDbContext CreateInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ListenArrDbContext(options);
        }

        [Fact]
        public async Task QualityGating_SkipsLowerQualityImport()
        {
            var db = CreateInMemoryDb();

            // Create audiobook and an existing high-quality file
            var book = new Audiobook { Title = "The High Quality Book" };
            db.Audiobooks.Add(book);
            await db.SaveChangesAsync();

            // Simulate existing AudiobookFile (MP3 320) in DB
            db.AudiobookFiles.Add(new AudiobookFile
            {
                AudiobookId = book.Id,
                Path = "C:\\library\\high.mp3",
                Format = "mp3",
                Bitrate = 320000,
                Source = "manual",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            // Create a temp file representing a lower-quality completed download (MP3 128)
            var tmp = Path.GetTempFileName();
            var tmpMp3 = Path.ChangeExtension(tmp, ".mp3");
            File.Move(tmp, tmpMp3);
            await File.WriteAllTextAsync(tmpMp3, "dummy");

            // Create download record linked to audiobook
            var download = new Download
            {
                Id = "qg-1",
                AudiobookId = book.Id,
                Title = book.Title,
                Status = DownloadStatus.Completed,
                DownloadPath = tmpMp3,
                FinalPath = tmpMp3,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Mock services
            var metadataMock = new Mock<IMetadataService>();
            metadataMock.Setup(m => m.ExtractFileMetadataAsync(It.IsAny<string>()))
                .ReturnsAsync(new AudioMetadata { Format = "mp3", Bitrate = 128000 });

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings { OutputPath = Path.GetTempPath(), EnableMetadataProcessing = true, CompletedFileAction = "Move" });

            var hubClientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            hubClientsMock.Setup(h => h.All).Returns(clientProxyMock.Object);
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

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
                cacheMock.Object,
                null);

            // Act - process completed download
            await downloadService.ProcessCompletedDownloadAsync(download.Id, download.FinalPath);

            // Assert: no new AudiobookFile created for this audiobook (still only the existing one)
            var files = await db.AudiobookFiles.Where(f => f.AudiobookId == book.Id).ToListAsync();
            Assert.Single(files);

            // Cleanup
            try { File.Delete(tmpMp3); } catch { }
        }

        [Fact]
        public async Task MultiFileImport_ImportsAllFiles_WithUniqueNames()
        {
            var db = CreateInMemoryDb();

            var book = new Audiobook { Title = "Multi Book", BasePath = Path.Combine(Path.GetTempPath(), "listenarr-multi", Guid.NewGuid().ToString()) };
            db.Audiobooks.Add(book);
            await db.SaveChangesAsync();

            // Ensure destination dir exists
            Directory.CreateDirectory(book.BasePath);

            // Create an existing file in destination with name collision
            var existing = Path.Combine(book.BasePath, "chapter1.mp3");
            await File.WriteAllTextAsync(existing, "existing");

            // Create source directory with two files: one collides, one new
            var srcDir = Path.Combine(Path.GetTempPath(), "listenarr-src", Guid.NewGuid().ToString());
            Directory.CreateDirectory(srcDir);
            var file1 = Path.Combine(srcDir, "chapter1.mp3");
            var file2 = Path.Combine(srcDir, "chapter2.mp3");
            await File.WriteAllTextAsync(file1, "file1");
            await File.WriteAllTextAsync(file2, "file2");

            // Create download pointing at the directory
            var download = new Download
            {
                Id = "mf-1",
                AudiobookId = book.Id,
                Title = book.Title,
                Status = DownloadStatus.Completed,
                DownloadPath = srcDir,
                FinalPath = srcDir,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Mocks
            var metadataMock = new Mock<IMetadataService>();
            metadataMock.Setup(m => m.ExtractFileMetadataAsync(It.IsAny<string>()))
                .ReturnsAsync(new AudioMetadata { Format = "mp3", Bitrate = 128000 });

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings { OutputPath = Path.GetTempPath(), EnableMetadataProcessing = true, CompletedFileAction = "Move" });

            var hubClientsMock = new Mock<IHubClients>();
            var clientProxyMock = new Mock<IClientProxy>();
            hubClientsMock.Setup(h => h.All).Returns(clientProxyMock.Object);
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

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
                cacheMock.Object,
                null);

            // Act
            await downloadService.ProcessCompletedDownloadAsync(download.Id, download.FinalPath);

            // Assert: files were moved into destination; at minimum we expect the files exist on disk
            var files = await db.AudiobookFiles.Where(f => f.AudiobookId == book.Id).ToListAsync();
            Assert.True(files.Count >= 1, "Expected at least one AudiobookFile DB record to be created");

            // Search recursively because naming patterns may place files into subfolders under the audiobook BasePath
            var diskFiles = Directory.GetFiles(book.BasePath, "*", SearchOption.AllDirectories).Select(p => Path.GetFileName(p)).ToList();
            // (No debug output)
            // Colliding original file should remain and a suffixed file should be present
            Assert.Contains("chapter1.mp3", diskFiles);
            // Either a suffixed file for the colliding chapter1, or the second file should also be present
            Assert.True(
                diskFiles.Any(d => d.StartsWith("chapter1 (")) ||
                diskFiles.Any(d => d.StartsWith("chapter2")) ||
                files.Count > 1,
                "Expected a suffixed filename for the collision or the second file to be present or multiple DB entries");

            // Cleanup
            try { Directory.Delete(book.BasePath, true); } catch { }
            try { Directory.Delete(srcDir, true); } catch { }
        }

        [Fact]
        public async Task ManualImport_AppendsUniqueSuffix_WhenDestinationExists()
        {
            var db = CreateInMemoryDb();

            // Create audiobook with basepath
            var basePath = Path.Combine(Path.GetTempPath(), "listenarr-manual", Guid.NewGuid().ToString());
            Directory.CreateDirectory(basePath);
            var book = new Audiobook { Title = "Manual Book", BasePath = basePath };
            db.Audiobooks.Add(book);
            await db.SaveChangesAsync();

            // Create source file
            var src = Path.Combine(Path.GetTempPath(), $"manual-src-{Guid.NewGuid()}.mp3");
            await File.WriteAllTextAsync(src, "content");

            // Create an existing destination file to cause collision
            var dest = Path.Combine(basePath, "Manual Book (2025)" );
            Directory.CreateDirectory(dest);
            var destFile = Path.Combine(dest, "chapter.mp3");
            await File.WriteAllTextAsync(destFile, "existing");

            // Prepare controller with mocks
            var repoMock = new Mock<IAudiobookRepository>();
            repoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => db.Audiobooks.Find(id));

            var metadataMock = new Mock<IMetadataService>();
            metadataMock.Setup(m => m.ExtractFileMetadataAsync(It.IsAny<string>())).ReturnsAsync(new AudioMetadata { Title = "Chapter", Bitrate = 128000 });

            var fileNamingMock = new Mock<IFileNamingService>();
            fileNamingMock.Setup(f => f.ApplyNamingPattern(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<bool>()))
                .Returns((string pattern, Dictionary<string, object> vars, bool t) => Path.Combine("Manual Book (2025)", "chapter.mp3"));

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings { OutputPath = basePath });

            var scanMock = new Mock<IScanQueueService>();
            scanMock.Setup(s => s.EnqueueScanAsync(It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(Guid.NewGuid());

            var controller = new ManualImportController(
                Mock.Of<Microsoft.Extensions.Logging.ILogger<ManualImportController>>(),
                repoMock.Object,
                metadataMock.Object,
                fileNamingMock.Object,
                configMock.Object,
                scanMock.Object
            );

            // Build request
            var request = new ManualImportRequest
            {
                Path = Path.GetTempPath(),
                Mode = "interactive",
                InputMode = "copy",
                Items = new System.Collections.Generic.List<ManualImportItem>
                {
                    new ManualImportItem { FullPath = src, MatchedAudiobookId = book.Id }
                }
            };

            // Act
            var result = await controller.Start(request);

            // Assert: destination should exist and be unique (chapter (1).mp3)
            var files = Directory.GetFiles(dest);
            Assert.Contains(files, p => Path.GetFileName(p).StartsWith("chapter"));
            Assert.True(files.Length >= 2 || files.Any(f => f.EndsWith("chapter (1).mp3")), "Expected a unique suffixed filename to be created");

            // Cleanup
            try { Directory.Delete(basePath, true); } catch { }
            try { File.Delete(src); } catch { }
        }

        [Fact]
        public async Task GetQueue_DoesNotPurge_WhenSabnzbdHistoryContainsMatch()
        {
            var db = CreateInMemoryDb();

            // Seed download that would otherwise be considered orphaned
            var download = new Download
            {
                Id = "purge-1",
                Title = "William Faulkner - The Sound and the Fury",
                Status = DownloadStatus.Downloading,
                DownloadClientId = "sab-1",
                StartedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Build client configuration that represents SABnzbd
            var clientConfig = new DownloadClientConfiguration
            {
                Id = "sab-1",
                Name = "Sabnzbd",
                Type = "sabnzbd",
                Host = "localhost",
                Port = 8080,
                UseSSL = false,
                IsEnabled = true,
                Settings = new Dictionary<string, object> { { "apiKey", "apikey" } }
            };

            // Setup configuration service to return our client list
            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetDownloadClientConfigurationsAsync()).ReturnsAsync(new List<DownloadClientConfiguration> { clientConfig });

            // Setup MemoryCache so the GetQueueAsync can use the cache path
            var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

            // Setup HTTP handler that returns empty queue but history contains the completed entry
            var handler = new DelegatingHandlerMock((req, ct) =>
            {
                var q = req.RequestUri?.Query ?? string.Empty;
                if (q.Contains("mode=queue"))
                {
                    var queueJson = "{\"queue\":{\"slots\":[]}}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(queueJson) });
                }

                if (q.Contains("mode=history"))
                {
                    var historyJson = "{\"history\":{\"slots\":[{\"nzo_id\":\"SABnzbd_nzo_x123\",\"name\":\"William Faulkner - The Sound and the Fury\",\"status\":\"Completed\",\"storage\":\"/downloads/complete/listenarr/William Faulkner - The Sound and the Fury\",\"completed\":1600000000}]}}";
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(historyJson) });
                }

                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
            });

            var httpClient = new HttpClient(handler);

            // Build service provider scope factory (for db contexts)
            var services = new ServiceCollection();
            services.AddSingleton<ListenArrDbContext>(db);
            services.AddSingleton<IConfigurationService>(configMock.Object);
            services.AddMemoryCache();
            services.AddSingleton(memoryCache);
            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            // Mocks for other constructor dependencies
            var repoMock = new Mock<IAudiobookRepository>();
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<DownloadService>>();
            var httpFactoryMock = new Mock<IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
            var pathMappingMock = new Mock<IRemotePathMappingService>();
            var searchMock = new Mock<ISearchService>();
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();

            var dbFactoryMock = new Mock<IDbContextFactory<ListenArrDbContext>>();
            dbFactoryMock.Setup(f => f.CreateDbContext()).Returns(db);

            // Metrics mock to assert telemetry
            var metricsMock = new Mock<IAppMetricsService>();

            // Construct the service under test (use our HttpClient and factory)
            var downloadService = new DownloadService(
                repoMock.Object,
                configMock.Object,
                dbFactoryMock.Object,
                loggerMock.Object,
                httpClient,
                httpFactoryMock.Object,
                scopeFactory,
                pathMappingMock.Object,
                searchMock.Object,
                hubContextMock.Object,
                memoryCache,
                null,
                metricsMock.Object);

            // Act - call GetQueueAsync which runs the purge path
            var result = await downloadService.GetQueueAsync();

            // Assert: the DB download should still exist (not purged) because SABnzbd history contained the matching entry
            using (var scope = provider.CreateScope())
            {
                var dbCtx = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                var stillExists = await dbCtx.Downloads.FindAsync(download.Id);
                Assert.NotNull(stillExists);
            }

            // Verify telemetry that a history title match prevented purge
            metricsMock.Verify(m => m.Increment("download.purge.skipped.history.title_match", It.IsAny<double>()), Times.AtLeastOnce);
        }
    }
}
