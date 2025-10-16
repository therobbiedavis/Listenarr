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

namespace Listenarr.Api.Models
{
    public enum DownloadStatus
    {
        Queued,
        Downloading,
        Paused,
        Completed,
        Failed,
        Processing,
        Ready
    }

    public class Download
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int? AudiobookId { get; set; } // Link to Audiobook record for metadata
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        
        // Enhanced identifying metadata for better matching
        public string? Asin { get; set; } // Amazon identifier - most reliable
        public string? Isbn { get; set; } // ISBN - very reliable
        public string? Series { get; set; } // Series name
        public string? SeriesNumber { get; set; } // Book number in series
        public string? Publisher { get; set; }
        public string? Language { get; set; }
        public int? Runtime { get; set; } // Runtime in minutes
        public long? ExpectedFileSize { get; set; } // Expected file size from search result
        public string OriginalUrl { get; set; } = string.Empty;
        public DownloadStatus Status { get; set; }
        public decimal Progress { get; set; }
        public long TotalSize { get; set; }
        public long DownloadedSize { get; set; }
        public string DownloadPath { get; set; } = string.Empty;
        public string FinalPath { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public string DownloadClientId { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Represents a download item in the queue with live status from download clients
    /// </summary>
    public class QueueItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Author { get; set; }
        public string? Series { get; set; }
        public string? SeriesNumber { get; set; }
        public string Quality { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // downloading, paused, queued, completed, failed
        public double Progress { get; set; } // 0-100
        public long Size { get; set; } // in bytes
        public long Downloaded { get; set; } // in bytes
        public double DownloadSpeed { get; set; } // bytes per second
        public int? Eta { get; set; } // seconds remaining
        public string? Indexer { get; set; }
        public string DownloadClient { get; set; } = string.Empty;
        public string DownloadClientId { get; set; } = string.Empty;
        public string DownloadClientType { get; set; } = string.Empty; // qbittorrent, transmission, etc
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
        public bool CanPause { get; set; } = true;
        public bool CanRemove { get; set; } = true;
        public int? Seeders { get; set; }
        public int? Leechers { get; set; }
        public double? Ratio { get; set; }
        
        /// <summary>
        /// The path as reported by the download client (may be in different mount point)
        /// </summary>
        public string? RemotePath { get; set; }
        
        /// <summary>
        /// The path translated for Listenarr's filesystem (after applying remote path mapping)
        /// </summary>
        public string? LocalPath { get; set; }
    }
}