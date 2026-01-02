using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Listenarr.Api.Services;
using Listenarr.Infrastructure.Models;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Tests
{
    public class MoveBackgroundServiceTests
    {
        [Fact(Timeout = 20000)]
        public async Task MoveBackgroundService_PerformsMoveAndUpdatesDb()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ListenArrDbContext>(opts => opts.UseInMemoryDatabase("test_db_move_background"));
            services.AddSingleton<IMoveQueueService, MoveQueueService>();
            services.AddSingleton<MoveBackgroundService>();

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var db = provider.GetRequiredService<ListenArrDbContext>();

            // Create source with files
            var src = Path.Combine(Path.GetTempPath(), "listenarr_test_src_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(src);
            var nested = Path.Combine(src, "Nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(src, "file1.txt"), "one");
            File.WriteAllText(Path.Combine(nested, "file2.txt"), "two");

            // Audiobook record uses src
            var ab = new Audiobook { Title = "MoveTest", BasePath = src };
            db.Audiobooks.Add(ab);
            await db.SaveChangesAsync();

            // Snapshot source timestamps before move
            var srcFile1 = Path.Combine(src, "file1.txt");
            var srcFile2 = Path.Combine(src, "Nested", "file2.txt");
            var srcFile1WriteUtc = File.GetLastWriteTimeUtc(srcFile1);
            var srcFile2WriteUtc = File.GetLastWriteTimeUtc(srcFile2);

            var moveQueue = provider.GetRequiredService<IMoveQueueService>();
            var bg = provider.GetRequiredService<MoveBackgroundService>();

            // Destination
            var dst = Path.Combine(Path.GetTempPath(), "listenarr_test_dst_" + Guid.NewGuid().ToString("N"));

            // Start the background service
            await bg.StartAsync(CancellationToken.None);

            // Enqueue move
            var jobId = await moveQueue.EnqueueMoveAsync(ab.Id, dst, src);

            // Poll for job completion (timeout ~15s)
            var succeeded = false;
            for (int i = 0; i < 60; i++)
            {
                if (moveQueue.TryGetJob(jobId, out var job) && string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    succeeded = true; break;
                }
                await Task.Delay(250, CancellationToken.None);
            }

            // Stop background service
            await bg.StopAsync(CancellationToken.None);

            Assert.True(succeeded, "Move job did not complete in time");

            // Verify destination has files and source removed
            Assert.True(Directory.Exists(dst));
            Assert.True(File.Exists(Path.Combine(dst, "file1.txt")));
            Assert.True(File.Exists(Path.Combine(dst, "Nested", "file2.txt")));
            Assert.False(Directory.Exists(src));

            // Verify timestamps preserved (took snapshots before move)
            var dstFile1 = Path.Combine(dst, "file1.txt");
            var dstFile2 = Path.Combine(dst, "Nested", "file2.txt");

            Assert.Equal(srcFile1WriteUtc, File.GetLastWriteTimeUtc(dstFile1));
            Assert.Equal(srcFile2WriteUtc, File.GetLastWriteTimeUtc(dstFile2));

            // Verify DB base path updated
            using (var scope = scopeFactory.CreateScope())
            {
                var db2 = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                var ab2 = await db2.Audiobooks.FindAsync(ab.Id);
                Assert.Equal(Path.GetFullPath(dst), ab2.BasePath);
            }

            // Cleanup
            try { Directory.Delete(dst, true); } catch { }
        }
    }
}
