using System;
using System.ComponentModel.DataAnnotations;

namespace Listenarr.Api.Models
{
    public class AudiobookFile
    {
        [Key]
        public int Id { get; set; }

        public int AudiobookId { get; set; }
        public Audiobook? Audiobook { get; set; }

        // Full path to the file on disk
        public string? Path { get; set; }

        // Size in bytes
        public long? Size { get; set; }

        // Duration in seconds
        public double? DurationSeconds { get; set; }

        // Format name (e.g., m4b, mp3, flac)
        public string? Format { get; set; }

        // Bitrate in bits per second
        public int? Bitrate { get; set; }

        // Sample rate in Hz
        public int? SampleRate { get; set; }

        // Number of audio channels
        public int? Channels { get; set; }

        // When this file record was created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Optional source or notes (e.g., DDL, qBittorrent, NZB)
        public string? Source { get; set; }
    }
}
