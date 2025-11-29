using System;

namespace Listenarr.Api.Models
{
    public class ProcessExecutionLog
    {
        public int Id { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        // Optional source tag to identify where the process was launched from (e.g. "PlaywrightInstall", "FfmpegInstaller")
        public string? Source { get; set; }

        // Executable/file and arguments
        public string? FileName { get; set; }
        public string? Arguments { get; set; }

        // Result
        public int? ExitCode { get; set; }
        public bool TimedOut { get; set; }
        public string? Stdout { get; set; }
        public string? Stderr { get; set; }

        // Duration in milliseconds (optional)
        public int? DurationMs { get; set; }
    }
}
