using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Listenarr.Api.Controllers;
using Listenarr.Api.Models;
using Listenarr.Api.Services;

namespace Listenarr.Api.Tests
{
    public class ManualImport_MultiFileCollisionTests
    {
        [Fact]
        public async Task InteractiveManualImport_MultipleFiles_ResolvesCollisionsWithinBatch()
        {
            // Setup DB-like audiobook object
            var basePath = Path.Combine(Path.GetTempPath(), "listenarr-manual-batch", Guid.NewGuid().ToString());
            Directory.CreateDirectory(basePath);

            var book = new Audiobook { Id = 42, Title = "Batch Book", BasePath = basePath };

            // Create two source files
            var srcDir = Path.Combine(Path.GetTempPath(), "listenarr-manual-src", Guid.NewGuid().ToString());
            Directory.CreateDirectory(srcDir);
            var src1 = Path.Combine(srcDir, "one.mp3");
            var src2 = Path.Combine(srcDir, "two.mp3");
            await File.WriteAllTextAsync(src1, "one");
            await File.WriteAllTextAsync(src2, "two");

            // Mocks
            var repoMock = new Mock<IAudiobookRepository>();
            repoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => id == book.Id ? book : null);

            var metadataMock = new Mock<IMetadataService>();
            metadataMock.Setup(m => m.ExtractFileMetadataAsync(It.IsAny<string>())).ReturnsAsync(new AudioMetadata { Title = "Chapter", Bitrate = 128000 });

            var fileNamingMock = new Mock<IFileNamingService>();
            // For manual import pattern {Title} we want the generated relative path to be the book title (no extra folders)
            fileNamingMock.Setup(f => f.ApplyNamingPattern(It.IsAny<string>(), It.IsAny<System.Collections.Generic.Dictionary<string, object>>(), It.IsAny<bool>()))
                .Returns((string pattern, System.Collections.Generic.Dictionary<string, object> vars, bool t) => vars.ContainsKey("Title") ? vars["Title"].ToString() ?? "Batch Book" : "Batch Book");

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

            var request = new ManualImportRequest
            {
                Path = srcDir,
                Mode = "interactive",
                InputMode = "copy",
                Items = new System.Collections.Generic.List<ManualImportItem>
                {
                    new ManualImportItem { FullPath = src1, MatchedAudiobookId = book.Id },
                    new ManualImportItem { FullPath = src2, MatchedAudiobookId = book.Id }
                }
            };

            // Act
            var result = await controller.Start(request);

            // Assert: both files should exist in the audiobook base path, second should have a suffix if name collided
            var diskFiles = Directory.GetFiles(basePath, "*", SearchOption.AllDirectories).Select(p => Path.GetFileName(p)).ToList();

            Assert.Contains(diskFiles, f => f.Equals("Batch Book.mp3", StringComparison.OrdinalIgnoreCase) || f.StartsWith("Batch Book"));
            // Expect at least two files (the second should be suffixed)
            Assert.True(diskFiles.Count >= 2, "Expected at least two files in destination (one suffixed for the collision)");

            // Cleanup
            try { Directory.Delete(basePath, true); } catch { }
            try { Directory.Delete(srcDir, true); } catch { }
        }
    }
}
