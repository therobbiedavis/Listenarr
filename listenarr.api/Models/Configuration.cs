namespace Listenarr.Api.Models
{
    public class ApiConfiguration
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "torrent" or "nzb"
        public bool IsEnabled { get; set; } = true;
        public int Priority { get; set; } = 1;
        public Dictionary<string, string> Headers { get; set; } = new();
        public Dictionary<string, string> Parameters { get; set; } = new();
        public string? RateLimitPerMinute { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsed { get; set; }
    }

    public class DownloadClientConfiguration
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "qbittorrent", "transmission", "sabnzbd", "nzbget"
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DownloadPath { get; set; } = string.Empty;
        public bool UseSSL { get; set; } = false;
        public bool IsEnabled { get; set; } = true;
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    public class ApplicationSettings
    {
        public string OutputPath { get; set; } = string.Empty;
        public string FileNamingPattern { get; set; } = "{Artist}/{Album}/{TrackNumber:00} - {Title}";
        public bool EnableMetadataProcessing { get; set; } = true;
        public bool EnableCoverArtDownload { get; set; } = true;
        public string AudnexusApiUrl { get; set; } = "https://api.audnex.us";
        public int MaxConcurrentDownloads { get; set; } = 3;
        public int PollingIntervalSeconds { get; set; } = 30;
        public bool EnableNotifications { get; set; } = false;
        public List<string> AllowedFileExtensions { get; set; } = new() { ".mp3", ".flac", ".m4a", ".m4b", ".ogg" };
    }
}