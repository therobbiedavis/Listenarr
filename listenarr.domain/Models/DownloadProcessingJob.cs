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

using System.ComponentModel.DataAnnotations;

namespace Listenarr.Domain.Models
{
    public enum ProcessingJobStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Retry
    }

    public enum ProcessingJobType
    {
        MoveOrCopyFile,
        ExtractMetadata,
        GenerateFileName,
        CreateAudiobookFile,
        NotifyCompletion
    }

    /// <summary>
    /// Represents a post-processing job for completed downloads
    /// </summary>
    public class DownloadProcessingJob
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The download this job is processing
        /// </summary>
        [Required]
        public string DownloadId { get; set; } = string.Empty;

        /// <summary>
        /// Type of processing job
        /// </summary>
        public ProcessingJobType JobType { get; set; }

        /// <summary>
        /// Current status of the job
        /// </summary>
        public ProcessingJobStatus Status { get; set; } = ProcessingJobStatus.Pending;

        /// <summary>
        /// Priority of the job (higher = more important)
        /// </summary>
        public int Priority { get; set; } = 5;

        /// <summary>
        /// Source file path (before processing)
        /// </summary>
        public string? SourcePath { get; set; }

        /// <summary>
        /// Destination file path (after processing)
        /// </summary>
        public string? DestinationPath { get; set; }

        /// <summary>
        /// Download client ID for path mapping
        /// </summary>
        public string? DownloadClientId { get; set; }

        /// <summary>
        /// Number of retry attempts made
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// Maximum number of retries allowed
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Error message if job failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// When the job was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When processing started
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When the job was completed (successfully or failed permanently)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// When to retry next (for failed jobs)
        /// </summary>
        public DateTime? NextRetryAt { get; set; }

        /// <summary>
        /// Additional job-specific data (stored as JSON)
        /// </summary>
        public Dictionary<string, object> JobData { get; set; } = new();

        /// <summary>
        /// Processing log entries
        /// </summary>
        public List<string> ProcessingLog { get; set; } = new();

        /// <summary>
        /// Add a log entry with timestamp
        /// </summary>
        public void AddLogEntry(string message)
        {
            ProcessingLog.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}");
        }

        /// <summary>
        /// Mark job as failed with error message
        /// </summary>
        public void MarkAsFailed(string errorMessage)
        {
            Status = ProcessingJobStatus.Failed;
            ErrorMessage = errorMessage;
            CompletedAt = DateTime.UtcNow;
            AddLogEntry($"Job failed: {errorMessage}");
        }

        /// <summary>
        /// Mark job as completed successfully
        /// </summary>
        public void MarkAsCompleted()
        {
            Status = ProcessingJobStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            AddLogEntry("Job completed successfully");
        }

        /// <summary>
        /// Schedule job for retry with exponential backoff
        /// </summary>
        public void ScheduleRetry()
        {
            if (RetryCount >= MaxRetries)
            {
                MarkAsFailed($"Max retries ({MaxRetries}) exceeded");
                return;
            }

            RetryCount++;
            Status = ProcessingJobStatus.Retry;

            // Exponential backoff: 30s, 2m, 8m, etc.
            var backoffMinutes = Math.Pow(2, RetryCount) * 0.5; // 0.5, 1, 2, 4, 8 minutes
            NextRetryAt = DateTime.UtcNow.AddMinutes(backoffMinutes);

            AddLogEntry($"Scheduled for retry #{RetryCount} at {NextRetryAt}");
        }
    }
}
