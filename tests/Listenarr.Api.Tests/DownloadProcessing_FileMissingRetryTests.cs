using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Listenarr.Api.Services;
using Listenarr.Api.Models;

namespace Listenarr.Api.Tests
{
    public class DownloadProcessing_FileMissingRetryTests
    {
        [Fact]
        public async Task ProcessMoveOrCopy_IfSourceMissing_SchedulesRetryAndRecordsMetric()
        {
            var dbOptions = new DbContextOptionsBuilder<ListenArrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new ListenArrDbContext(dbOptions);

            // Create temp source file then delete it to simulate race
            var sourceFile = Path.Combine(Path.GetTempPath(), $"dl-missing-{Guid.NewGuid()}.mp3");
            await File.WriteAllTextAsync(sourceFile, "test");

            // Destination directory must exist for background service to attempt operations
            var destRoot = Path.Combine(Path.GetTempPath(), $"dl-dest-{Guid.NewGuid()}");
            Directory.CreateDirectory(destRoot);

            // Add a download record and a processing job
            var dl = new Download
            {
                Id = "missing-test-1",
                Status = DownloadStatus.Completed,
                DownloadPath = sourceFile,
                FinalPath = sourceFile,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
            db.Downloads.Add(dl);
            await db.SaveChangesAsync();

            // Setup DI + services
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(db);

            var configMock = new Mock<IConfigurationService>();
            configMock.Setup(c => c.GetApplicationSettingsAsync()).ReturnsAsync(new ApplicationSettings
            {
                OutputPath = destRoot,
                CompletedFileAction = "Move",
                EnableMetadataProcessing = false
            });
            services.AddSingleton<IConfigurationService>(configMock.Object);

            var downloadServiceMock = new Mock<IDownloadService>();
            services.AddSingleton<IDownloadService>(downloadServiceMock.Object);

            // Queue service uses DbContext and logger - register real instance
            services.AddScoped<IDownloadProcessingQueueService, DownloadProcessingQueueService>();

            var metricsMock = new Mock<IAppMetricsService>();
            services.AddSingleton<IAppMetricsService>(metricsMock.Object);

            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<DownloadProcessingBackgroundService>>();

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            var queueService = provider.GetRequiredService<IDownloadProcessingQueueService>();

            // Enqueue the job pointing to the source file
            var jobId = await queueService.QueueDownloadProcessingAsync(dl.Id, sourceFile, null);
            var job = await queueService.GetJobAsync(jobId);

            // Delete the source to simulate disappearance before processing
            try { File.Delete(sourceFile); } catch { }

            // Create the background service instance and invoke the private ProcessMoveOrCopyJobAsync
            var svc = new DownloadProcessingBackgroundService(scopeFactory, loggerMock.Object, metricsMock.Object);

            // Set job to processing (the outer loop normally does this)
            job.Status = ProcessingJobStatus.Processing;
            job.StartedAt = DateTime.UtcNow;
            await queueService.UpdateJobAsync(job);

            using var scope = provider.CreateScope();

            var method = typeof(DownloadProcessingBackgroundService).GetMethod("ProcessMoveOrCopyJobAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            // Invoke and await the returned Task
            var task = (Task)method!.Invoke(svc, new object[] { job, scope, CancellationToken.None })!;
            await task;

            // Persist job updates (ProcessQueueAsync usually updates job at the end)
            await queueService.UpdateJobAsync(job);

            // Reload job
            var updated = await queueService.GetJobAsync(job.Id);
            Assert.NotNull(updated);
            Assert.Equal(ProcessingJobStatus.Retry, updated!.Status);
            Assert.True(updated.RetryCount >= 1);
            Assert.Contains("not found", updated.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            // Ensure metrics increment was recorded for missing source
            metricsMock.Verify(m => m.Increment("processing.source_missing", It.IsAny<double>()), Times.AtLeastOnce);

            // Cleanup created temp destination dir
            try { Directory.Delete(destRoot, true); } catch { }
        }
    }
}
