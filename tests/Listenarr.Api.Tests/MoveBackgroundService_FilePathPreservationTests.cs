using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Moq;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Tests
{
    public class MoveBackgroundService_FilePathPreservationTests
    {
        [Fact(Timeout = 20000)]
        public async Task MoveBackgroundService_UpdatesLegacyFilePath_WhenFileExistsInTarget()
        {
            var services = new ServiceCollection();
            // Enable debug logging for diagnostics if the test fails
            services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));
            var dbRoot = new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot();
            var dbName = Guid.NewGuid().ToString();
            services.AddDbContext<ListenArrDbContext>(opts => opts.UseInMemoryDatabase(dbName, dbRoot));

            services.AddSingleton<IMoveQueueService, MoveQueueService>();
            services.AddSingleton<IScanQueueService, ScanQueueService>();
            services.AddSingleton<MoveBackgroundService>();
            // Register real history repo so move path will add history entries
            services.AddScoped<Listenarr.Infrastructure.Repositories.IHistoryRepository, Listenarr.Infrastructure.Repositories.HistoryRepository>();

            // Minimal services needed for AudioFileService later
            services.AddSingleton<MetadataExtractionLimiter>();
            services.AddMemoryCache();

            // Register a simple metadata service mock
            var metadataMock = new Mock<IMetadataService>();
            metadataMock.Setup(m => m.ExtractFileMetadataAsync(It.IsAny<string>())).ReturnsAsync(new AudioMetadata { Duration = TimeSpan.FromSeconds(1), Format = "m4b" });
            services.AddSingleton<IMetadataService>(metadataMock.Object);

            var provider = services.BuildServiceProvider();
            using var rootScope = provider.CreateScope();
            var db = rootScope.ServiceProvider.GetRequiredService<ListenArrDbContext>();

            // Create source and target dirs
            var src = Path.Combine(Path.GetTempPath(), "listenarr_test_move_src_" + Guid.NewGuid().ToString("N"));
            var dst = Path.Combine(Path.GetTempPath(), "listenarr_test_move_dst_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(src);
            Directory.CreateDirectory(dst);

            var audioFileName = "dune.m4b";
            var srcFile = Path.Combine(src, audioFileName);
            await File.WriteAllTextAsync(srcFile, "dummy");

            var ab = new Audiobook { Title = "MoveFilePathTest", BasePath = src, FilePath = srcFile };
            db.Audiobooks.Add(ab);
            await db.SaveChangesAsync();

            var moveQueue = provider.GetRequiredService<IMoveQueueService>();
            var bg = provider.GetRequiredService<MoveBackgroundService>();

            // Start background service
            await bg.StartAsync(CancellationToken.None);

            // Enqueue move (include source so move uses our exact directory)
            var jobId = await moveQueue.EnqueueMoveAsync(ab.Id, dst, src);

            // Poll for completion
            var succeeded = false;
            for (int i = 0; i < 60; i++)
            {
                if (moveQueue.TryGetJob(jobId, out var job) && string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    succeeded = true; break;
                }
                await Task.Delay(250, CancellationToken.None);
            }

            await bg.StopAsync(CancellationToken.None);

            Assert.True(succeeded, "Move job did not complete in time");

            // Refresh audiobook from DB using a new scope so we get the latest values written by the background scope
            using var postScope = provider.CreateScope();
            var dbAfter = postScope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
            var fresh = await dbAfter.Audiobooks.FirstOrDefaultAsync(a => a.Id == ab.Id);
            Assert.NotNull(fresh);

            var expectedNewFilePath = Path.GetFullPath(Path.Combine(dst, Path.GetRelativePath(src, srcFile)));

            // The file should have been moved to target
            Assert.True(File.Exists(expectedNewFilePath), "Moved file not found at expected target path");
            // Original should not exist
            Assert.False(File.Exists(srcFile), "Source file should have been deleted after move");

            // The audiobook's legacy FilePath should have been updated to the new path
            Assert.Equal(expectedNewFilePath, fresh.FilePath);

            // Now verify AudioFileService will accept/associate the moved file
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<AudioFileService>>();
            var audioSvc = new AudioFileService(scopeFactory, loggerMock.Object, provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(), provider.GetRequiredService<MetadataExtractionLimiter>());

            var created = await audioSvc.EnsureAudiobookFileAsync(fresh.Id, expectedNewFilePath, "test");
            Assert.True(created, "AudioFileService failed to associate moved file even though FilePath was updated");

            var fileRecord = await db.AudiobookFiles.FirstOrDefaultAsync(f => f.AudiobookId == fresh.Id && f.Path == expectedNewFilePath);
            Assert.NotNull(fileRecord);

            // Cleanup
            try { Directory.Delete(dst, true); } catch { }
        }
    }
}
