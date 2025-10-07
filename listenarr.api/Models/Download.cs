namespace Listenarr.Api.Models
{
    public enum DownloadStatus
    {
        Queued,
        Downloading,
        Completed,
        Failed,
        Processing,
        Ready
    }

    public class Download
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
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
    }
}