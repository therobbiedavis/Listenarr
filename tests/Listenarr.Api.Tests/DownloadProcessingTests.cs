using System;
using System.Threading.Tasks;
using Listenarr.Api.Models;
using Listenarr.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class DownloadProcessingTests
    {
        private ListenArrDbContext CreateInMemoryDb(string dbName)
        {
            var options = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ListenArrDbContext(options);
        }

        [Fact]
        public async Task ProcessCompletedDownload_CreatesAudiobookFile()
        {
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateInMemoryDb(dbName);

            // Seed an audiobook and a download
            var audiobook = new Audiobook { Title = "Test Book" };
            context.Audiobooks.Add(audiobook);
            await context.SaveChangesAsync();

            var download = new Download
            {
                Id = "dl-test-1",
                AudiobookId = audiobook.Id,
                Title = "DL Test",
                DownloadPath = "/tmp/test.m4b",
                FinalPath = "/tmp/test.m4b",
                DownloadClientId = "DDL",
                StartedAt = DateTime.UtcNow,
                Status = DownloadStatus.Downloading,
                Progress = 0,
                TotalSize = 100
            };
            context.Downloads.Add(download);
            await context.SaveChangesAsync();

            // Build service provider for DownloadService dependencies
            var services = new ServiceCollection();
            services.AddSingleton<ListenArrDbContext>(context);
            services.AddLogging();
            services.AddHttpClient();
            services.AddSingleton<IServiceScopeFactory>(sp => sp.GetRequiredService<IServiceScopeFactory>());

            var provider = services.BuildServiceProvider();

            // Create DownloadService with minimal dependencies (some may be null but not used)
            var downloadService = new DownloadService(
                audiobookRepository: null!,
                configurationService: null!,
                dbContext: context,
                logger: provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DownloadService>>(),
                httpClient: provider.GetRequiredService<System.Net.Http.HttpClient>(),
                serviceScopeFactory: provider.GetRequiredService<IServiceScopeFactory>(),
                pathMappingService: null!,
                searchService: null!
            );

            // Ensure the file exists in the test (simulate file size)
            var testPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.bin");
            await File.WriteAllTextAsync(testPath, "dummy");
            var finalPath = testPath;

            // Call the processing method
            await downloadService.ProcessCompletedDownloadAsync(download.Id, finalPath);

            // Assert that an AudiobookFile was added
            var files = await context.AudiobookFiles.ToListAsync();
            Assert.Single(files);
            Assert.Equal(audiobook.Id, files[0].AudiobookId);
            Assert.Equal(finalPath, files[0].Path);
            Assert.True(files[0].Size > 0);
        }
    }
}
