using System;
using System.Threading.Tasks;

namespace Listenarr.Api.Services
{
    public class ScanJob
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int AudiobookId { get; set; }
        public string? Path { get; set; }
        public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Queued";
        public string? Error { get; set; }
    }

    public interface IScanQueueService
    {
        Task<Guid> EnqueueScanAsync(int audiobookId, string? path = null);
        Task<Guid?> RequeueScanAsync(Guid jobId);
        bool TryGetJob(Guid id, out ScanJob? job);
        void UpdateJobStatus(Guid id, string status, string? error = null, int? found = null, int? created = null);
    }
}
