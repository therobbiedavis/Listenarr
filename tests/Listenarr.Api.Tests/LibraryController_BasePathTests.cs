using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Listenarr.Api.Controllers;
using Listenarr.Api.Models;
using Listenarr.Api.Services;
using System.IO;

namespace Listenarr.Api.Tests
{
    public class LibraryController_BasePathTests
    {
        [Fact]
        public void ComputeAudiobookBaseDirectoryFromPattern_NonSeriesBook_ReturnsCorrectPath()
        {
            // Arrange
            var audiobook = new Audiobook
            {
                Title = "The Buffalo Hunter Hunter",
                Authors = new List<string> { "Stephen Graham Jones" },
                PublishYear = "2025",
                Series = null, // No series
                SeriesNumber = null
            };

            var rootPath = "/server/mnt/drive/Audiobooks";
            var fileNamingPattern = "{Author}/{Series}/{DiskNumber:00} - {ChapterNumber:00} - {Title}"; // Default pattern

            // Mock dependencies
            var mockRepo = new Mock<IAudiobookRepository>();
            var mockImageCache = new Mock<IImageCacheService>();
            var mockLogger = new Mock<ILogger<LibraryController>>();
            var mockScanQueue = new Mock<IScanQueueService>();

            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var dbContext = new ListenArrDbContext(options);

            var mockFileNamingService = new Mock<IFileNamingService>();
            mockFileNamingService
                .Setup(x => x.ApplyNamingPattern(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), false))
                .Returns((string pattern, Dictionary<string, object> vars, bool sanitize) =>
                {
                    // For non-series book, the method now uses fixed pattern "{Author}/{Title} ({Year})"
                    if (pattern == "{Author}/{Title} ({Year})")
                    {
                        return "Stephen Graham Jones/The Buffalo Hunter Hunter (2025)";
                    }
                    return pattern;
                });

            var serviceProvider = new Mock<IServiceProvider>();

            // Create controller instance
            var controller = new LibraryController(
                mockRepo.Object,
                mockImageCache.Object,
                mockLogger.Object,
                dbContext,
                serviceProvider.Object,
                mockFileNamingService.Object,
                mockScanQueue.Object);

            // Get the private method using reflection
            var method = typeof(LibraryController).GetMethod("ComputeAudiobookBaseDirectoryFromPattern",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = (string)method.Invoke(controller, new object[] { audiobook, rootPath, fileNamingPattern });

            // Assert
            var expected = Path.Combine("/server/mnt/drive/Audiobooks", "Stephen Graham Jones/The Buffalo Hunter Hunter (2025)");
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ComputeAudiobookBaseDirectoryFromPattern_SeriesBook_ReturnsCorrectPath()
        {
            // Arrange
            var audiobook = new Audiobook
            {
                Title = "The Gunslinger",
                Authors = new List<string> { "Stephen King" },
                PublishYear = "1982",
                Series = "The Dark Tower",
                SeriesNumber = "1"
            };

            var rootPath = "/server/mnt/drive/Audiobooks";
            var fileNamingPattern = "{Author}/{Series}/{DiskNumber:00} - {ChapterNumber:00} - {Title}"; // Default pattern

            // Mock dependencies
            var mockRepo = new Mock<IAudiobookRepository>();
            var mockImageCache = new Mock<IImageCacheService>();
            var mockLogger = new Mock<ILogger<LibraryController>>();
            var mockScanQueue = new Mock<IScanQueueService>();

            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var dbContext = new ListenArrDbContext(options);

            var mockFileNamingService = new Mock<IFileNamingService>();
            mockFileNamingService
                .Setup(x => x.ApplyNamingPattern(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), false))
                .Returns((string pattern, Dictionary<string, object> vars, bool sanitize) =>
                {
                    // For series book, the method now uses fixed pattern "{Author}/{Series}/{Title} ({Year})"
                    if (pattern == "{Author}/{Series}/{Title} ({Year})")
                    {
                        return "Stephen King/The Dark Tower/The Gunslinger (1982)";
                    }
                    return pattern;
                });

            var serviceProvider = new Mock<IServiceProvider>();

            // Create controller instance
            var controller = new LibraryController(
                mockRepo.Object,
                mockImageCache.Object,
                mockLogger.Object,
                dbContext,
                serviceProvider.Object,
                mockFileNamingService.Object,
                mockScanQueue.Object);

            // Get the private method using reflection
            var method = typeof(LibraryController).GetMethod("ComputeAudiobookBaseDirectoryFromPattern",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = (string)method.Invoke(controller, new object[] { audiobook, rootPath, fileNamingPattern });

            // Assert
            var expected = Path.Combine("/server/mnt/drive/Audiobooks", "Stephen King/The Dark Tower/The Gunslinger (1982)");
            Assert.Equal(expected, result);
        }
    }
}