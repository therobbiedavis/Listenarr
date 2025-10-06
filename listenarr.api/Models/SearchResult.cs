namespace Listenarr.Api.Models
{
    public class SearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public long Size { get; set; }
        public int Seeders { get; set; }
        public int Leechers { get; set; }
        public string MagnetLink { get; set; } = string.Empty;
        public string TorrentUrl { get; set; } = string.Empty;
        public string NzbUrl { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime PublishedDate { get; set; }
        public string Quality { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        
        // Additional properties for enhanced audiobook metadata
        public string? Description { get; set; }
        public string? Publisher { get; set; }
        public string? Language { get; set; }
        public int? Runtime { get; set; }
        public string? Narrator { get; set; }
        public string? ImageUrl { get; set; }
        public string? Asin { get; set; }
        public string? Series { get; set; }
        public string? SeriesNumber { get; set; }
        // Indicates this result had a successful full metadata enrichment pass (Audible product scrape)
        public bool IsEnriched { get; set; }
    }
}