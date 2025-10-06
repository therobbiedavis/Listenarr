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
}