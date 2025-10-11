using System.Text.Json.Serialization;

namespace Listenarr.Api.Models
{
    public class StartupConfig
    {
        // Minimal set of keys from the user's requested config.json. Keep names flexible.
        public string? LogLevel { get; set; }
        public bool? EnableSsl { get; set; }
        public int? Port { get; set; }
        public int? SslPort { get; set; }
        public string? UrlBase { get; set; }
        public string? BindAddress { get; set; }
        public string? ApiKey { get; set; }
        public string? AuthenticationMethod { get; set; }
        public string? UpdateMechanism { get; set; }
        public bool? LaunchBrowser { get; set; }
        public string? Branch { get; set; }
        public string? InstanceName { get; set; }
        public int? SyslogPort { get; set; }
        public bool? AnalyticsEnabled { get; set; }

        // This is the new flag the user asked for. Accept both boolean or string-like values via JSON.
        [JsonPropertyName("AuthenticationRequired")]
        public string? AuthenticationRequired { get; set; }

        public string? SslCertPath { get; set; }
        public string? SslCertPassword { get; set; }

        // FFmpeg/ffprobe installer configuration
        public FfmpegConfig? Ffmpeg { get; set; }
    }

    public class FfmpegConfig
    {
        // Provider key: e.g., "johnvansickle", "gyan", "evermeet", or "github:<owner>/<repo>"
        public string? Provider { get; set; }

        // Optional explicit asset name or tag to pin a release, e.g., "ffmpeg-6.0.zip" or "6.0"
        public string? ReleaseOverride { get; set; }

        // Optional URL template for checksum file discovery (e.g., GitHub releases assets or a SHA file)
        public string? ChecksumUrl { get; set; }

        // Optional architecture hint, e.g., "x86_64", "arm64"
        public string? Arch { get; set; }
    }
}
