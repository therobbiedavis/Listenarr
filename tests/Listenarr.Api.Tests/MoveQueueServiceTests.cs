using System;
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
    public class MoveQueueServiceTests
    {
        [Fact]
        public async Task UpdateJobStatus_PersistsAndUpdatesInMemory()
        {
            var services = new ServiceCollection();
            services.AddDbContext<ListenArrDbContext>(opts => opts.UseInMemoryDatabase("test_db_movejob"));
            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var logger = new NullLogger<MoveQueueService>();

            var svc = new MoveQueueService(logger, scopeFactory);

            // Enqueue a job (creates DB entry)
            var jobId = await svc.EnqueueMoveAsync(1, "C:\\dest\\path", "C:\\src\\path");

            // Initially the job should be queued
            Assert.True(svc.TryGetJob(jobId, out var job1));
            Assert.Equal("Queued", job1!.Status);

            // Update status to Processing
            svc.UpdateJobStatus(jobId, "Processing", null);
            Assert.True(svc.TryGetJob(jobId, out var job2));
            Assert.Equal("Processing", job2!.Status);

            // Verify persisted in DB
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ListenArrDbContext>();
                var dbJob = await db.MoveJobs.FindAsync(jobId);
                Assert.NotNull(dbJob);
                Assert.Equal("Processing", dbJob!.Status);
            }
        }
    }
}
