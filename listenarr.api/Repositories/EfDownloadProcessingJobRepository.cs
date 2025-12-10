using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Repositories
{
    public class EfDownloadProcessingJobRepository : IDownloadProcessingJobRepository
    {
        private readonly IDbContextFactory<ListenArrDbContext> _dbFactory;
        private readonly ILogger<EfDownloadProcessingJobRepository> _logger;

        public EfDownloadProcessingJobRepository(IDbContextFactory<ListenArrDbContext> dbFactory, ILogger<EfDownloadProcessingJobRepository> logger)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<string>> GetPendingDownloadIdsAsync(IEnumerable<string> completedDownloadIds)
        {
            var ids = (completedDownloadIds ?? Array.Empty<string>()).ToList();
            if (!ids.Any()) return new List<string>();

            var ctx = await _dbFactory.CreateDbContextAsync();
            return await ctx.DownloadProcessingJobs
                .Where(j => ids.Contains(j.DownloadId) && (j.Status == ProcessingJobStatus.Pending || j.Status == ProcessingJobStatus.Processing || j.Status == ProcessingJobStatus.Retry))
                .Select(j => j.DownloadId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<string>> GetAllJobDownloadIdsAsync(IEnumerable<string> completedDownloadIds)
        {
            var ids = (completedDownloadIds ?? Array.Empty<string>()).ToList();
            if (!ids.Any()) return new List<string>();

            var ctx = await _dbFactory.CreateDbContextAsync();
            return await ctx.DownloadProcessingJobs
                .Where(j => ids.Contains(j.DownloadId))
                .Select(j => j.DownloadId)
                .Distinct()
                .ToListAsync();
        }
    }
}
