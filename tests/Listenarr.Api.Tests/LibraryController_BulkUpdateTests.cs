using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    public class LibraryController_BulkUpdateTests
    {
        [Fact]
        public async Task BulkUpdate_ApplyRootMonitoredQuality_ReturnsPerIdResultsAndPersistsChanges()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var dbContext = new ListenArrDbContext(options);

            // Create two audiobooks in DB
            var a1 = new Audiobook
            {
                Title = "Book A",
                Authors = new List<string> { "Author A" },
                Monitored = false,
                QualityProfileId = null
            };

            var a2 = new Audiobook
            {
                Title = "Book B",
                Authors = new List<string> { "Author B" },
                Monitored = false,
                QualityProfileId = null
            };

            await dbContext.Audiobooks.AddAsync(a1);
            await dbContext.Audiobooks.AddAsync(a2);
            await dbContext.SaveChangesAsync();

            // Mock repository to return our DB entries by id
            var mockRepo = new Mock<IAudiobookRepository>();
            mockRepo.Setup(r => r.GetByIdAsync(a1.Id)).ReturnsAsync(a1);
            mockRepo.Setup(r => r.GetByIdAsync(a2.Id)).ReturnsAsync(a2);

            var mockImageCache = new Mock<IImageCacheService>();
            var mockLogger = new Mock<ILogger<LibraryController>>();

            var mockFileNaming = new Mock<IFileNamingService>();
            mockFileNaming
                .Setup(f => f.ApplyNamingPattern(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), false))
                .Returns((string pattern, Dictionary<string, object> vars, bool sanitize) =>
                {
                    var author = vars.ContainsKey("Author") ? vars["Author"]?.ToString() ?? "Unknown" : "Unknown";
                    var title = vars.ContainsKey("Title") ? vars["Title"]?.ToString() ?? "Unknown" : "Unknown";
                    return Path.Combine(author, title).Replace("\\", "/");
                });

            // Configuration service providing a FileNamingPattern (not strictly used by our mock but kept consistent)
            var tempRoot = Path.Combine(Path.GetTempPath(), "listenarr-bulk-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var mockConfigService = new Mock<IConfigurationService>();
            mockConfigService.Setup(c => c.GetApplicationSettingsAsync())
                .ReturnsAsync(new ApplicationSettings { OutputPath = tempRoot, FileNamingPattern = "{Author}/{Title}" });

            var services = new ServiceCollection();
            services.AddSingleton<IConfigurationService>(mockConfigService.Object);
            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            // Create controller instance
            var controller = new LibraryController(
                mockRepo.Object,
                mockImageCache.Object,
                mockLogger.Object,
                dbContext,
                scopeFactory,
                mockFileNaming.Object);

            // Build request: update monitored + qualityProfileId + rootFolder (include a non-existent id)
            var request = new LibraryController.BulkUpdateRequest
            {
                Ids = new List<int> { a1.Id, 999999 },
                Updates = new Dictionary<string, object>
                {
                    { "monitored", true },
                    { "qualityProfileId", 42 },
                    { "rootFolder", tempRoot }
                }
            };

            // Act
            var actionResult = await controller.BulkUpdateAudiobooks(request);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(actionResult);

            // Inspect returned JSON for per-id results
            var json = JsonSerializer.Serialize(ok.Value);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("results", out var resultsElem));
            Assert.Equal(2, resultsElem.GetArrayLength());

            // First result should be success for existing audiobook
            var first = resultsElem[0];
            Assert.Equal(a1.Id, first.GetProperty("id").GetInt32());
            Assert.True(first.GetProperty("success").GetBoolean());
            Assert.True(first.GetProperty("errors").GetArrayLength() == 0);

            // Second result should indicate not found
            var second = resultsElem[1];
            Assert.Equal(999999, second.GetProperty("id").GetInt32());
            Assert.False(second.GetProperty("success").GetBoolean());
            Assert.True(second.GetProperty("errors").GetArrayLength() >= 1);

            // Verify DB changes persisted for a1
            var storedA1 = await dbContext.Audiobooks.FindAsync(a1.Id);
            Assert.NotNull(storedA1);
            Assert.True(storedA1.Monitored);
            Assert.Equal(42, storedA1.QualityProfileId);
            Assert.False(string.IsNullOrWhiteSpace(storedA1.BasePath));
            Assert.StartsWith(tempRoot, storedA1.BasePath);
            Assert.Contains("Author A", storedA1.BasePath);
            Assert.Contains("Book A", storedA1.BasePath);

            // Verify history entry exists for the change
            var histories = dbContext.History.Where(h => h.AudiobookId == a1.Id).ToList();
            Assert.True(histories.Count >= 1);

            // Cleanup
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }
}
