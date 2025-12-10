using System;
using System.Collections.Generic;
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
    public class LibraryController_AddToLibraryTests
    {
        [Fact]
        public async Task AddToLibrary_UsesLegacyAuthorField_PopulatesAuthorsAndBasePath()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var dbContext = new ListenArrDbContext(options);

            var mockRepo = new Mock<IAudiobookRepository>();
            mockRepo.Setup(r => r.AddAsync(It.IsAny<Audiobook>()))
                .Returns<Audiobook>(async (ab) =>
                {
                    await dbContext.Audiobooks.AddAsync(ab);
                    await dbContext.SaveChangesAsync();
                });

            var mockImageCache = new Mock<IImageCacheService>();
            var mockLogger = new Mock<ILogger<LibraryController>>();

            var mockFileNaming = new Mock<IFileNamingService>();
            mockFileNaming
                .Setup(f => f.ApplyNamingPattern(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), false))
                .Returns((string pattern, Dictionary<string, object> vars, bool t) =>
                {
                    // Simulate FileNamingService producing an Author/Title relative path
                    var author = vars.ContainsKey("Author") ? vars["Author"]?.ToString() ?? "Unknown" : "Unknown";
                    var title = vars.ContainsKey("Title") ? vars["Title"]?.ToString() ?? "Unknown" : "Unknown";
                    return Path.Combine(author, title).Replace("\\", "/");
                });

            // Configuration service providing an OutputPath root
            var tempRoot = Path.Combine(Path.GetTempPath(), "listenarr-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var mockConfigService = new Mock<IConfigurationService>();
            mockConfigService.Setup(c => c.GetApplicationSettingsAsync())
                .ReturnsAsync(new ApplicationSettings { OutputPath = tempRoot, FileNamingPattern = "{Author}/{Title}" });

            var mockQualityProfile = new Mock<IQualityProfileService>();
            mockQualityProfile.Setup(q => q.GetDefaultAsync()).ReturnsAsync((QualityProfile?)null);

            var services = new ServiceCollection();
            services.AddSingleton<IConfigurationService>(mockConfigService.Object);
            services.AddSingleton<IQualityProfileService>(mockQualityProfile.Object);
            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var controller = new LibraryController(
                mockRepo.Object,
                mockImageCache.Object,
                mockLogger.Object,
                dbContext,
                scopeFactory,
                mockFileNaming.Object);

            var request = new LibraryController.AddToLibraryRequest
            {
                Metadata = new AudibleBookMetadata
                {
                    Title = "Legacy Title",
                    Author = "Legacy Author"
                },
                Monitored = true
            };

            // Act
            var actionResult = await controller.AddToLibrary(request);

            // Assert
            Assert.IsType<OkObjectResult>(actionResult);

            var stored = await dbContext.Audiobooks.FirstOrDefaultAsync();
            Assert.NotNull(stored);
            Assert.NotNull(stored.Authors);
            Assert.Contains("Legacy Author", stored.Authors);
            Assert.False(string.IsNullOrWhiteSpace(stored.BasePath));
            Assert.StartsWith(tempRoot, stored.BasePath);
            Assert.Contains("Legacy Author", stored.BasePath);
            Assert.Contains("Legacy Title", stored.BasePath);

            // Cleanup
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }
}
