using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Listenarr.Api.Models
{
    public class MoveJob
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public int AudiobookId { get; set; }
        public string? RequestedPath { get; set; }
        public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Queued";
        public string? Error { get; set; }
        public int AttemptCount { get; set; } = 0;
        public DateTime? UpdatedAt { get; set; }
        // Optional source path snapshot provided at enqueue time. Persist this so jobs
        // remain durable and can be inspected / resumed across restarts.
        public string? SourcePath { get; set; }
    }
}
