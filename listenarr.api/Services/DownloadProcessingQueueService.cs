/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using Listenarr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Service for managing download post-processing queue with retry capabilities
    /// </summary>
    public class DownloadProcessingQueueService : IDownloadProcessingQueueService
    {
        private readonly ListenArrDbContext _context;
        private readonly ILogger<DownloadProcessingQueueService> _logger;

        public DownloadProcessingQueueService(
            ListenArrDbContext context,
            ILogger<DownloadProcessingQueueService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<string> QueueDownloadProcessingAsync(string downloadId, string sourcePath, string? downloadClientId = null)
        {
            var job = new DownloadProcessingJob
            {
                DownloadId = downloadId,
                JobType = ProcessingJobType.MoveOrCopyFile,
                SourcePath = sourcePath,
                DownloadClientId = downloadClientId,
                Priority = 5, // Normal priority
                Status = ProcessingJobStatus.Pending
            };

            job.AddLogEntry($"Queued for post-processing: {sourcePath}");

            _context.DownloadProcessingJobs.Add(job);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Queued download {DownloadId} for post-processing: {JobId}", downloadId, job.Id);

            return job.Id;
        }

        public async Task<DownloadProcessingJob?> GetNextJobAsync()
        {
            return await _context.DownloadProcessingJobs
                .Where(j => j.Status == ProcessingJobStatus.Pending)
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<DownloadProcessingJob>> GetRetryJobsAsync()
        {
            var now = DateTime.UtcNow;
            return await _context.DownloadProcessingJobs
                .Where(j => j.Status == ProcessingJobStatus.Retry && 
                           j.NextRetryAt.HasValue && 
                           j.NextRetryAt <= now)
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.NextRetryAt)
                .ToListAsync();
        }

        public async Task UpdateJobAsync(DownloadProcessingJob job)
        {
            _context.DownloadProcessingJobs.Update(job);
            await _context.SaveChangesAsync();
        }

        public async Task<DownloadProcessingJob?> GetJobAsync(string jobId)
        {
            return await _context.DownloadProcessingJobs.FindAsync(jobId);
        }

        public async Task<List<DownloadProcessingJob>> GetJobsForDownloadAsync(string downloadId)
        {
            return await _context.DownloadProcessingJobs
                .Where(j => j.DownloadId == downloadId)
                .OrderBy(j => j.CreatedAt)
                .ToListAsync();
        }

        public async Task<QueueStats> GetStatsAsync()
        {
            var stats = await _context.DownloadProcessingJobs
                .GroupBy(j => j.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var oldestPending = await _context.DownloadProcessingJobs
                .Where(j => j.Status == ProcessingJobStatus.Pending)
                .OrderBy(j => j.CreatedAt)
                .Select(j => j.CreatedAt)
                .FirstOrDefaultAsync();

            var result = new QueueStats
            {
                OldestPendingJob = oldestPending == default ? null : oldestPending
            };

            foreach (var stat in stats)
            {
                switch (stat.Status)
                {
                    case ProcessingJobStatus.Pending:
                        result.PendingJobs = stat.Count;
                        break;
                    case ProcessingJobStatus.Processing:
                        result.ProcessingJobs = stat.Count;
                        break;
                    case ProcessingJobStatus.Completed:
                        result.CompletedJobs = stat.Count;
                        break;
                    case ProcessingJobStatus.Failed:
                        result.FailedJobs = stat.Count;
                        break;
                    case ProcessingJobStatus.Retry:
                        result.RetryJobs = stat.Count;
                        break;
                }
                result.TotalJobs += stat.Count;
            }

            return result;
        }

        public async Task CleanupOldJobsAsync(int retentionDays = 7)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            
            var oldJobs = await _context.DownloadProcessingJobs
                .Where(j => (j.Status == ProcessingJobStatus.Completed || j.Status == ProcessingJobStatus.Failed) &&
                           j.CompletedAt.HasValue && j.CompletedAt < cutoffDate)
                .ToListAsync();

            if (oldJobs.Any())
            {
                _context.DownloadProcessingJobs.RemoveRange(oldJobs);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Cleaned up {Count} old processing jobs older than {Days} days", 
                    oldJobs.Count, retentionDays);
            }
        }

        public async Task<List<DownloadProcessingJob>> GetRecentActivityAsync(int count = 50)
        {
            return await _context.DownloadProcessingJobs
                .OrderByDescending(j => j.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
    }
}