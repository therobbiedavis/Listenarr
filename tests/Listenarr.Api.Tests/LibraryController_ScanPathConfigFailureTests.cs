using System;
using System.IO;
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
    public class LibraryController_ScanPathConfigFailureTests
    {
        [Fact]
        public async Task ScanAudiobook_ConfigUnavailable_NoBasePath_Returns500()
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var dbContext = new ListenArrDbContext(options);
            var mockRepo = new Mock<IAudiobookRepository>();
            var mockImageCache = new Mock<IImageCacheService>();
            var mockLogger = new Mock<ILogger<LibraryController>>();
            var mockFileNaming = new Mock<IFileNamingService>();

            var services = new ServiceCollection();
            // Configuration service that throws to simulate inability to load settings
            var mockConfig = new Mock<IConfigurationService>();
            mockConfig.Setup(c => c.GetApplicationSettingsAsync()).ThrowsAsync(new Exception("config failure"));
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
            mockRootFolderSvc.Setup(r => r.GetAllAsync()).ReturnsAsync(new System.Collections.Generic.List<RootFolder>());

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

            var request = new LibraryController.ScanRequest { Path = Path.Combine(Path.GetTempPath(), "somepath") };

            var result = await controller.ScanAudiobookFiles(ab.Id, request);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, obj.StatusCode);
            Assert.Contains("Failed to determine a safe scan path", obj.Value?.ToString() ?? string.Empty);
        }
    }
}
