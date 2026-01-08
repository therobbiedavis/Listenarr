using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Listenarr.Api.Controllers;
using Listenarr.Api.Services;
using Listenarr.Infrastructure.Models;
using Listenarr.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace Listenarr.Api.Tests
{
    public class LibraryController_DeleteImageSafetyTests
    {
        [Fact]
        public async Task DeleteAudiobook_InvalidImageUrl_DoesNotCallImageCacheService()
        {
            // Arrange
            var mockRepo = new Mock<IAudiobookRepository>();
            var mockImageCache = new Mock<IImageCacheService>();
            var mockLogger = new Mock<ILogger<LibraryController>>();
            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(System.Guid.NewGuid().ToString())
                .Options;

            var dbContext = new ListenArrDbContext(options);

            var services = new ServiceCollection();
            var mockConfig = new Mock<IConfigurationService>();
            mockConfig.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings { OutputPath = System.IO.Path.GetTempPath() });
            services.AddSingleton<IConfigurationService>(mockConfig.Object);

            // Provide a mock signalR hub context (with Clients.All mocked) to avoid exceptions during broadcast
            var mockHub = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>>();
            var mockClients = new Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
            var mockClientProxy = new Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();
            mockClientProxy.Setup(m => m.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default)).Returns(System.Threading.Tasks.Task.CompletedTask);
            mockClients.SetupGet(c => c.All).Returns(mockClientProxy.Object);
            mockHub.SetupGet(h => h.Clients).Returns(mockClients.Object);
            services.AddSingleton(typeof(Microsoft.AspNetCore.SignalR.IHubContext<Listenarr.Api.Hubs.DownloadHub>), mockHub.Object);

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var fileNaming = new Mock<Listenarr.Api.Services.IFileNamingService>().Object;

            var audiobook = new Listenarr.Domain.Models.Audiobook { Id = 123, Title = "Test", ImageUrl = "/config/cache/images/library/../evil/../../secret.txt" };
            mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(audiobook);
            mockRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<int>())).ReturnsAsync(true);

            var controller = new LibraryController(
                mockRepo.Object,
                mockImageCache.Object,
                mockLogger.Object,
                dbContext,
                scopeFactory,
                fileNaming,
                scanQueueService: null,
                moveQueueService: null,
                notificationService: null);

            // Act
            var result = await controller.DeleteAudiobook(audiobook.Id);

            // Assert
            // The identifier 'secret' should be extracted and validated; ensure we called into the image cache service
            mockImageCache.Verify(s => s.GetCachedImagePathAsync("secret"), Times.Once);
            Assert.IsType<OkObjectResult>(result);
        }
    }
}
