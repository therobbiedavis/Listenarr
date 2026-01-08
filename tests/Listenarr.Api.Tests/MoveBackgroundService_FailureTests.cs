using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Listenarr.Api.Services;
using Listenarr.Infrastructure.Models;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Tests
{
    public class MoveBackgroundService_FailureTests
    {
        [Fact(Timeout = 20000)]
        public async Task MoveBackgroundService_Fails_WhenFileLocked_IncrementsAttemptCount()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ListenArrDbContext>(opts => opts.UseInMemoryDatabase("test_db_move_failure"));
            services.AddSingleton<IMoveQueueService, MoveQueueService>();
            services.AddSingleton<MoveBackgroundService>();

            var provider = services.BuildServiceProvider();
            var db = provider.GetRequiredService<ListenArrDbContext>();
            var moveQueue = provider.GetRequiredService<IMoveQueueService>();
            var bg = provider.GetRequiredService<MoveBackgroundService>();

            // Create source with a file
            var src = Path.Combine(Path.GetTempPath(), "listenarr_test_src_lock_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(src);
            var file = Path.Combine(src, "file_locked.txt");
            File.WriteAllText(file, "locked");

            var dst = Path.Combine(Path.GetTempPath(), "listenarr_test_dst_lock_" + Guid.NewGuid().ToString("N"));

            // Create a blocking file at the destination path so Directory.Move will fail
            var dstParent = Path.GetDirectoryName(dst) ?? Path.GetTempPath();
            Directory.CreateDirectory(dstParent);
            File.WriteAllText(dst, "block");

            var ab = new Audiobook { Title = "MoveFailTest", BasePath = src };
            db.Audiobooks.Add(ab);
            await db.SaveChangesAsync();

            // Start background service
            await bg.StartAsync(CancellationToken.None);

            var jobId = await moveQueue.EnqueueMoveAsync(ab.Id, dst, src);

            // Wait for job to fail
            var failed = false;
            for (int i = 0; i < 60; i++)
            {
                if (moveQueue.TryGetJob(jobId, out var job) && string.Equals(job.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    failed = true; break;
                }
                await Task.Delay(200, CancellationToken.None);
            }

            await bg.StopAsync(CancellationToken.None);

            Assert.True(failed, "Move job did not fail as expected when file was locked");

            // Check attempt count incremented in DB
            using (var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var db2 = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                var dbJob = await db2.MoveJobs.FindAsync(jobId);
                Assert.True(dbJob.AttemptCount > 0, "AttemptCount was not incremented on failure");
            }

            // Cleanup
            try { File.Delete(dst); } catch { }
            try { Directory.Delete(src, true); } catch { }
            try { Directory.Delete(dst, true); } catch { }
        }
    }
}
