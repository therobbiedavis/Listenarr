using System;

namespace Listenarr.Api.Services
{
    public class FileMoverOptions
    {
        // Enable or disable using robocopy as a fallback on Windows
        public bool EnableRobocopy { get; set; } = true;

        // Timeout for robocopy/process runner calls in milliseconds
        public int RobocopyTimeoutMs { get; set; } = 60000;

        // Retry configuration for move attempts (number of attempts)
        public int MaxRetries { get; set; } = 4;

        // Backoff (ms) initial and maximum
        public int MinBackoffMs { get; set; } = 1000;
        public int MaxBackoffMs { get; set; } = 8000;
    }
}
