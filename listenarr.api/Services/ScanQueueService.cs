using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading.Channels;
using System;

namespace Listenarr.Api.Services
{
    public class ScanQueueService : IScanQueueService
    {
        private readonly ConcurrentDictionary<Guid, ScanJob> _jobs = new();
        private readonly Channel<ScanJob> _channel = Channel.CreateUnbounded<ScanJob>();
        private readonly ILogger<ScanQueueService> _logger;

        public ScanQueueService(ILogger<ScanQueueService> logger)
        {
            _logger = logger;
        }

        public async Task<Guid> EnqueueScanAsync(int audiobookId, string? path = null)
        {
            var job = new ScanJob { AudiobookId = audiobookId, Path = path };
            _jobs[job.Id] = job;
            _logger.LogInformation("Enqueueing scan job {JobId} for audiobook {AudiobookId} (path: {Path})", job.Id, audiobookId, path);
            await _channel.Writer.WriteAsync(job);
            _logger.LogInformation("Scan job {JobId} written to channel", job.Id);
            return job.Id;
        }

        public bool TryGetJob(Guid id, out ScanJob? job) => _jobs.TryGetValue(id, out job);

        public ChannelReader<ScanJob> Reader => _channel.Reader;

        public void UpdateJobStatus(Guid id, string status, string? error = null, int? found = null, int? created = null)
        {
            if (_jobs.TryGetValue(id, out var job))
            {
                job.Status = status;
                job.Error = error;
                // store optional counters in the Error field or extend ScanJob if needed; keep simple for now
                _jobs[id] = job;
                _logger.LogInformation("Updated scan job {JobId} status to {Status}", id, status);
            }
            else
            {
                _logger.LogWarning("Attempted to update unknown scan job {JobId} to {Status}", id, status);
            }
        }

        public async Task<Guid?> RequeueScanAsync(Guid jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
            {
                _logger.LogWarning("Attempted to requeue unknown scan job {JobId}", jobId);
                return null;
            }

            // Allow requeue for Failed jobs or Completed (explicit re-run)
            if (!string.Equals(job.Status, "Failed", StringComparison.OrdinalIgnoreCase) && !string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase) && !string.Equals(job.Status, "Queued", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Scan job {JobId} has status {Status} and cannot be requeued", jobId, job.Status);
                return null;
            }

            var newJob = new ScanJob { AudiobookId = job.AudiobookId, Path = job.Path };
            _jobs[newJob.Id] = newJob;
            _logger.LogInformation("Requeueing scan job {OldJobId} as new job {NewJobId} for audiobook {AudiobookId}", jobId, newJob.Id, job.AudiobookId);
            await _channel.Writer.WriteAsync(newJob);
            return newJob.Id;
        }
    }
}
