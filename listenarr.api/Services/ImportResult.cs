using System;

namespace Listenarr.Api.Services
{
    public class ImportResult
    {
        public bool Success { get; set; }
        public string? SourcePath { get; set; }
        public string? FinalPath { get; set; }
        public string? Message { get; set; }
        public bool WasMoved { get; set; }
        public bool WasCopied { get; set; }
        public bool WasRegisteredToAudiobook { get; set; }
        public string? SkippedReason { get; set; }
        public DateTime? Timestamp { get; set; } = DateTime.UtcNow;
    }
}
