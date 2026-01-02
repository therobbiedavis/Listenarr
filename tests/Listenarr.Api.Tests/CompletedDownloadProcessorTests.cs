using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Listenarr.Api.Services;
using Listenarr.Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.IO.Compression;

namespace Listenarr.Api.Tests
{
    public class CompletedDownloadProcessorTests
    {
        [Fact]
        public async Task ProcessCompletedDownloadAsync_SingleFile_UpdatesFinalPathAndStatus()
        {
            // Arrange
            var downloadId = Guid.NewGuid().ToString();
            var finalPath = "C:\\temp\\audiobook.mp3";

            var repo = new TestDownloadRepository();
            await repo.AddAsync(new Download { Id = downloadId, Status = DownloadStatus.Downloading, AudiobookId = null });

            var fileFinalizer = new TestFileFinalizer(null);

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings());

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var scopeMock = new Mock<IServiceScope>();
            var spMock = new Mock<IServiceProvider>();
            spMock.Setup(sp => sp.GetService(typeof(ListenArrDbContext))).Returns(null);
            scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var importMock = new Mock<IImportService>();

            var queueMock = new Mock<IDownloadQueueService>();
            queueMock.Setup(q => q.GetQueueAsync()).ReturnsAsync(new List<Listenarr.Domain.Models.QueueItem>());

            var hubContextMock = new Mock<IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
            var loggerMock = new Mock<ILogger<CompletedDownloadProcessor>>();

            var archiveExtractor = new ArchiveExtractor(new Mock<ILogger<ArchiveExtractor>>().Object);

            var processor = new CompletedDownloadProcessor(repo, fileFinalizer, configMock.Object, scopeFactoryMock.Object, importMock.Object, archiveExtractor, queueMock.Object, hubContextMock.Object, loggerMock.Object);

            // Act
            await processor.ProcessCompletedDownloadAsync(downloadId, finalPath);

            // Assert
            var tracked = await repo.FindAsync(downloadId);
            Assert.NotNull(tracked);
            Assert.Equal(DownloadStatus.Completed, tracked!.Status);
            // TestFileFinalizer returns FinalPath equal to source when no import service; so FinalPath should be set
            Assert.Equal(finalPath, tracked.FinalPath);
        }

        [Fact]
        public async Task ProcessCompletedDownloadAsync_Directory_InvokesDirectoryImport()
        {
            // Arrange
            var downloadId = Guid.NewGuid().ToString();
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(tempDir);
            var filePath = System.IO.Path.Combine(tempDir, "file1.mp3");
            System.IO.File.WriteAllText(filePath, "dummy");

            var repo = new TestDownloadRepository();
            await repo.AddAsync(new Download { Id = downloadId, Status = DownloadStatus.Downloading });

            var fileFinalizer = new TestFileFinalizer(null);

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings());

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var scopeMock = new Mock<IServiceScope>();
            var spMock = new Mock<IServiceProvider>();
            spMock.Setup(sp => sp.GetService(typeof(ListenArrDbContext))).Returns(null);
            scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var importMock = new Mock<IImportService>();
            var queueMock = new Mock<IDownloadQueueService>();
            queueMock.Setup(q => q.GetQueueAsync()).ReturnsAsync(new List<Listenarr.Domain.Models.QueueItem>());
            var hubContextMock = new Mock<IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
            var loggerMock = new Mock<ILogger<CompletedDownloadProcessor>>();
            var archiveExtractor = new ArchiveExtractor(new Mock<ILogger<ArchiveExtractor>>().Object);

            var processor = new CompletedDownloadProcessor(repo, fileFinalizer, configMock.Object, scopeFactoryMock.Object, importMock.Object, archiveExtractor, queueMock.Object, hubContextMock.Object, loggerMock.Object);

            // Act
            await processor.ProcessCompletedDownloadAsync(downloadId, tempDir);

            // Assert
            var tracked = await repo.FindAsync(downloadId);
            Assert.NotNull(tracked);
            Assert.Equal(DownloadStatus.Completed, tracked!.Status);

            // cleanup
            try { System.IO.File.Delete(filePath); } catch { }
            try { System.IO.Directory.Delete(tempDir); } catch { }
        }

        [Fact]
        public async Task ProcessCompletedDownloadAsync_RecursiveDirectory_ImportsNestedFile()
        {
            var downloadId = Guid.NewGuid().ToString();
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            var nested = System.IO.Path.Combine(tempDir, "nested");
            System.IO.Directory.CreateDirectory(nested);
            var filePath = System.IO.Path.Combine(nested, "file2.mp3");
            System.IO.File.WriteAllText(filePath, "dummy");

            var repo = new TestDownloadRepository();
            await repo.AddAsync(new Download { Id = downloadId, Status = DownloadStatus.Downloading });

            var fileFinalizer = new TestFileFinalizer(null);

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings());

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var scopeMock = new Mock<IServiceScope>();
            var spMock = new Mock<IServiceProvider>();
            spMock.Setup(sp => sp.GetService(typeof(ListenArrDbContext))).Returns(null);
            scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var importMock = new Mock<IImportService>();
            var queueMock = new Mock<IDownloadQueueService>();
            queueMock.Setup(q => q.GetQueueAsync()).ReturnsAsync(new List<Listenarr.Domain.Models.QueueItem>());
            var hubContextMock = new Mock<IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
            var loggerMock = new Mock<ILogger<CompletedDownloadProcessor>>();
            var archiveExtractor = new ArchiveExtractor(new Mock<ILogger<ArchiveExtractor>>().Object);

            var processor = new CompletedDownloadProcessor(repo, fileFinalizer, configMock.Object, scopeFactoryMock.Object, importMock.Object, archiveExtractor, queueMock.Object, hubContextMock.Object, loggerMock.Object);

            await processor.ProcessCompletedDownloadAsync(downloadId, tempDir);

            var tracked = await repo.FindAsync(downloadId);
            Assert.NotNull(tracked);
            Assert.Equal(DownloadStatus.Completed, tracked!.Status);
            Assert.Equal(filePath, tracked.FinalPath);

            // cleanup
            try { System.IO.File.Delete(filePath); } catch { }
            try { System.IO.Directory.Delete(tempDir, true); } catch { }
        }

        [Fact]
        public async Task ProcessCompletedDownloadAsync_ArchiveExtraction_ImportsContainedFile()
        {
            var downloadId = Guid.NewGuid().ToString();
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            var inner = System.IO.Path.Combine(tempDir, "inner");
            System.IO.Directory.CreateDirectory(inner);
            var audioPath = System.IO.Path.Combine(inner, "audio.mp3");
            System.IO.File.WriteAllText(audioPath, "dummy");

            var zipPath = System.IO.Path.Combine(tempDir, "release.zip");
            ZipFile.CreateFromDirectory(inner, zipPath);

            var repo = new TestDownloadRepository();
            await repo.AddAsync(new Download { Id = downloadId, Status = DownloadStatus.Downloading });

            var fileFinalizer = new TestFileFinalizer(null);

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings());

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var scopeMock = new Mock<IServiceScope>();
            var spMock = new Mock<IServiceProvider>();
            spMock.Setup(sp => sp.GetService(typeof(ListenArrDbContext))).Returns(null);
            scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var importMock = new Mock<IImportService>();
            var queueMock = new Mock<IDownloadQueueService>();
            queueMock.Setup(q => q.GetQueueAsync()).ReturnsAsync(new List<Listenarr.Domain.Models.QueueItem>());
            var hubContextMock = new Mock<IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
            var loggerMock = new Mock<ILogger<CompletedDownloadProcessor>>();
            var archiveExtractor = new ArchiveExtractor(new Mock<ILogger<ArchiveExtractor>>().Object);

            var processor = new CompletedDownloadProcessor(repo, fileFinalizer, configMock.Object, scopeFactoryMock.Object, importMock.Object, archiveExtractor, queueMock.Object, hubContextMock.Object, loggerMock.Object);

            await processor.ProcessCompletedDownloadAsync(downloadId, zipPath);

            var tracked = await repo.FindAsync(downloadId);
            Assert.NotNull(tracked);
            Assert.Equal(DownloadStatus.Completed, tracked!.Status);
            // FinalPath should have been updated to the extracted audio file path
            Assert.Contains("audio.mp3", tracked.FinalPath ?? string.Empty);

            // cleanup
            try { System.IO.File.Delete(zipPath); } catch { }
            try { System.IO.Directory.Delete(tempDir, true); } catch { }
        }
    }
}
