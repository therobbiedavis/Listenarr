using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Listenarr.Api.Repositories;
using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Listenarr.Api.Tests
{
    public class TestDownloadProcessingJobRepository : IDownloadProcessingJobRepository
    {
        private readonly ListenArrDbContext? _db;

        public TestDownloadProcessingJobRepository(ListenArrDbContext? db = null)
        {
            _db = db;
        }

        public Task<List<string>> GetPendingDownloadIdsAsync(IEnumerable<string> completedDownloadIds)
        {
            var ids = (completedDownloadIds ?? Array.Empty<string>()).ToList();
            if (!_db?.DownloadProcessingJobs?.Any() == true)
            {
                // If no DB is available, return empty list (tests that need jobs should provide a DB)
                return Task.FromResult(new List<string>());
            }

            if (_db != null)
            {
                return _db.DownloadProcessingJobs
                    .Where(j => ids.Contains(j.DownloadId) && (j.Status == ProcessingJobStatus.Pending || j.Status == ProcessingJobStatus.Processing || j.Status == ProcessingJobStatus.Retry))
                    .Select(j => j.DownloadId)
                    .Distinct()
                    .ToListAsync();
            }

            return Task.FromResult(new List<string>());
        }

        public Task<List<string>> GetAllJobDownloadIdsAsync(IEnumerable<string> completedDownloadIds)
        {
            var ids = (completedDownloadIds ?? Array.Empty<string>()).ToList();
            if (!_db?.DownloadProcessingJobs?.Any() == true)
            {
                return Task.FromResult(new List<string>());
            }

            if (_db != null)
            {
                return _db.DownloadProcessingJobs
                    .Where(j => ids.Contains(j.DownloadId))
                    .Select(j => j.DownloadId)
                    .Distinct()
                    .ToListAsync();
            }

            return Task.FromResult(new List<string>());
        }
    }
}
