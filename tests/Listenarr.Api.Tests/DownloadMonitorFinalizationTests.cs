using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Listenarr.Api.Models;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;

namespace Listenarr.Api.Tests
{
    public class DownloadMonitorFinalizationTests
    {
        private ListenArrDbContext CreateInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ListenArrDbContext(options);
        }

        private ServiceProvider BuildServiceProvider(ListenArrDbContext db, Mock<IDownloadService> downloadServiceMock, ApplicationSettings settings)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ListenArrDbContext>(db);
            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(settings);
            services.AddSingleton<IConfigurationService>(configMock.Object);
            services.AddSingleton<IDownloadService>(downloadServiceMock.Object);
            // Provide a minimal file naming service that won't be used (metadata disabled in settings)
            var fileNamingMock = new Mock<IFileNamingService>();
            services.AddSingleton<IFileNamingService>(fileNamingMock.Object);
            // metadata service
            var metadataMock = new Mock<IMetadataService>();
            services.AddSingleton<IMetadataService>(metadataMock.Object);

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task FinalizeDownload_MovesFile_WhenSettingIsMove()
        {
            var db = CreateInMemoryDb();

            // Seed download
            var download = new Download
            {
                Id = "d1",
                Title = "Test Move",
                Status = DownloadStatus.Downloading,
                DownloadPath = string.Empty,
                FinalPath = string.Empty,
                StartedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Create source file
            var tempDir = Path.Combine(Path.GetTempPath(), "listenarr-test", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var sourceFile = Path.Combine(tempDir, "Test Move.m4b");
            await File.WriteAllTextAsync(sourceFile, "dummy");

            // Settings: move to output path
            var outDir = Path.Combine(Path.GetTempPath(), "listenarr-out", Guid.NewGuid().ToString());
            Directory.CreateDirectory(outDir);
            var settings = new ApplicationSettings { OutputPath = outDir, EnableMetadataProcessing = false, CompletedFileAction = "Move" };

            var downloadServiceMock = new Mock<IDownloadService>();
            downloadServiceMock.Setup(d => d.ProcessCompletedDownloadAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();

            var provider = BuildServiceProvider(db, downloadServiceMock, settings);
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var hubClientsMock = new Mock<IHubClients>();
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var loggerMock = new Mock<ILogger<DownloadMonitorService>>();

            var monitor = new DownloadMonitorService(scopeFactory, hubContextMock.Object, loggerMock.Object);

            // Invoke private FinalizeDownloadAsync via reflection
            var method = typeof(DownloadMonitorService).GetMethod("FinalizeDownloadAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var clientConfig = new DownloadClientConfiguration { Id = "c1", Name = "Local", DownloadPath = tempDir };

            var task = (Task?)method.Invoke(monitor, new object[] { download, tempDir, clientConfig, CancellationToken.None });
            if (task != null) await task;

            // Expect file moved to output
            var destFile = Path.Combine(outDir, Path.GetFileName(sourceFile));
            Assert.True(File.Exists(destFile));
            Assert.False(File.Exists(sourceFile));
            downloadServiceMock.Verify(d => d.ProcessCompletedDownloadAsync(download.Id, It.Is<string>(s => s == destFile)), Times.AtLeastOnce);
        }

        [Fact]
        public async Task FinalizeDownload_CopiesFile_WhenSettingIsCopy()
        {
            var db = CreateInMemoryDb();

            // Seed download
            var download = new Download
            {
                Id = "d2",
                Title = "Test Copy",
                Status = DownloadStatus.Downloading,
                DownloadPath = string.Empty,
                FinalPath = string.Empty,
                StartedAt = DateTime.UtcNow
            };
            db.Downloads.Add(download);
            await db.SaveChangesAsync();

            // Create source file
            var tempDir = Path.Combine(Path.GetTempPath(), "listenarr-test", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var sourceFile = Path.Combine(tempDir, "Test Copy.m4b");
            await File.WriteAllTextAsync(sourceFile, "dummy");

            // Settings: copy to output path
            var outDir = Path.Combine(Path.GetTempPath(), "listenarr-out", Guid.NewGuid().ToString());
            Directory.CreateDirectory(outDir);
            var settings = new ApplicationSettings { OutputPath = outDir, EnableMetadataProcessing = false, CompletedFileAction = "Copy" };

            var downloadServiceMock = new Mock<IDownloadService>();
            downloadServiceMock.Setup(d => d.ProcessCompletedDownloadAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();

            var provider = BuildServiceProvider(db, downloadServiceMock, settings);
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var hubClientsMock = new Mock<IHubClients>();
            var hubContextMock = new Mock<IHubContext<DownloadHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(hubClientsMock.Object);

            var loggerMock = new Mock<ILogger<DownloadMonitorService>>();

            var monitor = new DownloadMonitorService(scopeFactory, hubContextMock.Object, loggerMock.Object);

            var method = typeof(DownloadMonitorService).GetMethod("FinalizeDownloadAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var clientConfig = new DownloadClientConfiguration { Id = "c2", Name = "Local", DownloadPath = tempDir };

            var task = (Task?)method.Invoke(monitor, new object[] { download, tempDir, clientConfig, CancellationToken.None });
            if (task != null) await task;

            // Expect file copied to output
            var destFile = Path.Combine(outDir, Path.GetFileName(sourceFile));
            Assert.True(File.Exists(destFile));
            Assert.True(File.Exists(sourceFile));
            downloadServiceMock.Verify(d => d.ProcessCompletedDownloadAsync(download.Id, It.Is<string>(s => s == destFile)), Times.AtLeastOnce);
        }
    }
}
