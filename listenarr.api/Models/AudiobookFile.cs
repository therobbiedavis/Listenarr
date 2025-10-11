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

        // When this file record was created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Optional source or notes (e.g., DDL, qBittorrent, NZB)
        public string? Source { get; set; }
    }
}
