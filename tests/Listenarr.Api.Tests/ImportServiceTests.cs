using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Tests
{
    public class ImportServiceTests
    {
        [Fact]
        public async Task ImportFilesFromDirectory_CreatesDestinationDirectory_WhenMissing()
        {
            // Arrange
            var outputRoot = Path.Combine(Path.GetTempPath(), $"import-out-{Guid.NewGuid()}");
            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);

            var sourceDir = Path.Combine(Path.GetTempPath(), $"import-src-{Guid.NewGuid()}");
            Directory.CreateDirectory(sourceDir);
            var file1 = Path.Combine(sourceDir, "track1.m4b");
            var file2 = Path.Combine(sourceDir, "track2.m4b");
            await File.WriteAllTextAsync(file1, "dummy");
            await File.WriteAllTextAsync(file2, "dummy");

            var settings = new ApplicationSettings { OutputPath = outputRoot, CompletedFileAction = "Move", EnableMetadataProcessing = false };

            // Build provider and register ImportService with an in-memory DB factory
            var provider = TestServiceFactory.BuildServiceProvider(services =>
            {
                var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options;

                var dbFactoryMock = new Mock<IDbContextFactory<ListenArrDbContext>>();
                dbFactoryMock.Setup(f => f.CreateDbContextAsync(It.IsAny<System.Threading.CancellationToken>()))
                    .ReturnsAsync(new ListenArrDbContext(options));

                services.AddSingleton<IDbContextFactory<ListenArrDbContext>>(dbFactoryMock.Object);
                services.AddSingleton<IFileNamingService>(new FileNamingService(new TestConfigurationService(), new NullLogger<FileNamingService>()));
                services.AddSingleton<IMetadataService>(new Mock<IMetadataService>().Object);
                // ImportService uses NullFileMover by default when not provided, which is fine for tests
                services.AddSingleton<IImportService>(sp => new ImportService(dbFactoryMock.Object, sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<IFileNamingService>(), sp.GetService<IMetadataService>(), new NullLogger<ImportService>()));
            });

            var importService = provider.GetRequiredService<IImportService>();

            // Act
            var results = await importService.ImportFilesFromDirectoryAsync("dl-1", null, new[] { file1, file2 }, settings);

            // Assert: destination directory created
            Assert.True(Directory.Exists(outputRoot));

            // At least one successful import result should be present
            Assert.True(results.Any(r => r.Success));

            // All successful results should point to files under the output root
            foreach (var r in results.Where(r => r.Success))
            {
                Assert.StartsWith(outputRoot.TrimEnd(Path.DirectorySeparatorChar), r.FinalPath, StringComparison.OrdinalIgnoreCase);
                Assert.True(File.Exists(r.FinalPath));
            }

            // Cleanup
            try { Directory.Delete(sourceDir, true); } catch { }
            try { Directory.Delete(outputRoot, true); } catch { }
        }
    }
}
