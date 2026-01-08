using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Listenarr.Api.Controllers;
using Listenarr.Domain.Models;
using Listenarr.Api.Services;
using Listenarr.Infrastructure.Models;

namespace Listenarr.Api.Tests
{
    public class LibraryController_ScanPathValidationTests
    {
        [Fact]
        public async Task ScanAudiobook_AllowsRequestPathWithinConfiguredRoot_ReturnsOk()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "listenarr-test-root-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var dbContext = new ListenArrDbContext(options);
            var mockRepo = new Mock<IAudiobookRepository>();
            var mockImageCache = new Mock<IImageCacheService>();
            var mockLogger = new Mock<ILogger<LibraryController>>();
            var mockFileNaming = new Mock<IFileNamingService>();

            var services = new ServiceCollection();
            var mockConfig = new Mock<IConfigurationService>();
            mockConfig.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings { OutputPath = Path.GetTempPath() });
            services.AddSingleton<IConfigurationService>(mockConfig.Object);

            // SignalR hub stubs
            var mockHub = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
            var mockClients = new Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
            var mockClientProxy = new Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();
            mockClientProxy.Setup(m => m.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default)).Returns(Task.CompletedTask);
            mockClients.SetupGet(c => c.All).Returns(mockClientProxy.Object);
            mockHub.SetupGet(h => h.Clients).Returns(mockClients.Object);
            services.AddSingleton(typeof(Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>), mockHub.Object);

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            // Add an audiobook with no BasePath so request.Path is allowed when it's within root folders
            var ab = new Audiobook { Title = "Test", BasePath = null };
            dbContext.Audiobooks.Add(ab);
            await dbContext.SaveChangesAsync();

            mockRepo.Setup(m => m.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => dbContext.Audiobooks.Find(id));

            // Mock root folder service to include our tempRoot
            var mockRootFolderSvc = new Mock<IRootFolderService>();
            mockRootFolderSvc.Setup(r => r.GetAllAsync()).ReturnsAsync(new System.Collections.Generic.List<RootFolder> {
                new RootFolder { Id = 1, Name = "root", Path = tempRoot }
            });

            var controller = new LibraryController(
                mockRepo.Object,
                mockImageCache.Object,
                mockLogger.Object,
                dbContext,
                scopeFactory,
                mockFileNaming.Object,
                null,
                null,
                null,
                mockRootFolderSvc.Object);

            var request = new LibraryController.ScanRequest { Path = tempRoot };

            var result = await controller.ScanAudiobookFiles(ab.Id, request);

            Assert.IsType<OkObjectResult>(result);
            var ok = (OkObjectResult)result;
            Assert.Equal(200, ok.StatusCode);
            Assert.Contains("No files found", ok.Value?.ToString() ?? string.Empty);
        }

        [Fact]
        public async Task ScanAudiobook_RejectsRequestPathOutsideConfiguredRoots_ReturnsBadRequest()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "listenarr-test-root-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            var other = Path.Combine(Path.GetTempPath(), "listenarr-other-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(other);

            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var dbContext = new ListenArrDbContext(options);
            var mockRepo = new Mock<IAudiobookRepository>();
            var mockImageCache = new Mock<IImageCacheService>();
            var mockLogger = new Mock<ILogger<LibraryController>>();
            var mockFileNaming = new Mock<IFileNamingService>();

            var services = new ServiceCollection();
            var mockConfig = new Mock<IConfigurationService>();
            // Use an output path that does NOT contain 'other' to ensure the requested path is outside configured roots
            mockConfig.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings { OutputPath = Path.Combine(Path.GetTempPath(), "different-root") });
            services.AddSingleton<IConfigurationService>(mockConfig.Object);

            var mockHub = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
            var mockClients = new Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
            var mockClientProxy = new Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();
            mockClientProxy.Setup(m => m.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default)).Returns(Task.CompletedTask);
            mockClients.SetupGet(c => c.All).Returns(mockClientProxy.Object);
            mockHub.SetupGet(h => h.Clients).Returns(mockClients.Object);
            services.AddSingleton(typeof(Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>), mockHub.Object);

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var ab = new Audiobook { Title = "Test", BasePath = null };
            dbContext.Audiobooks.Add(ab);
            await dbContext.SaveChangesAsync();

            mockRepo.Setup(m => m.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => dbContext.Audiobooks.Find(id));

            var mockRootFolderSvc = new Mock<IRootFolderService>();
            mockRootFolderSvc.Setup(r => r.GetAllAsync()).ReturnsAsync(new System.Collections.Generic.List<RootFolder> {
                new RootFolder { Id = 1, Name = "root", Path = tempRoot }
            });

            var controller = new LibraryController(
                mockRepo.Object,
                mockImageCache.Object,
                mockLogger.Object,
                dbContext,
                scopeFactory,
                mockFileNaming.Object,
                null,
                null,
                null,
                mockRootFolderSvc.Object);

            var request = new LibraryController.ScanRequest { Path = other };

            var result = await controller.ScanAudiobookFiles(ab.Id, request);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, bad.StatusCode);
            Assert.Contains("not within configured root folders", bad.Value?.ToString() ?? string.Empty);
        }
    }
}
