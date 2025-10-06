namespace Listenarr.Api.Models
{
    public class AudibleBookMetadata
    {
        // Use single canonical ASIN property to avoid JSON property name collisions
        public string? Asin { get; set; }
        public string? Source { get; set; } // "Audible" or "Amazon" to track metadata source
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public List<string>? Authors { get; set; }
        public string? ImageUrl { get; set; }
        public string? PublishYear { get; set; }
        public string? Series { get; set; }
        public string? SeriesNumber { get; set; }
        public string? Description { get; set; }
        public List<string>? Genres { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? Narrators { get; set; }
        public string? Isbn { get; set; }
    // (Asin moved to top to be the canonical ASIN property)
        public string? Publisher { get; set; }
        public string? Language { get; set; }
        public int? Runtime { get; set; }
        public string? Version { get; set; }
        public bool Explicit { get; set; }
        public bool Abridged { get; set; }
        // Legacy fields for compatibility
        public string? Author { get; set; }
        public string? Narrator { get; set; }
    }
}
