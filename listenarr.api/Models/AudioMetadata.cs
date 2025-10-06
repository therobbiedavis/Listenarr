namespace Listenarr.Api.Models
{
    public class AudioMetadata
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string AlbumArtist { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public int? Year { get; set; }
        public int? TrackNumber { get; set; }
        public int? TrackTotal { get; set; }
        public int? DiscNumber { get; set; }
        public int? DiscTotal { get; set; }
        public TimeSpan Duration { get; set; }
        public string Format { get; set; } = string.Empty;
        public int Bitrate { get; set; }
        public int SampleRate { get; set; }
        public string? Isbn { get; set; }
        public string? Asin { get; set; }
        public string? Description { get; set; }
        public string? Narrator { get; set; }
        public string? Publisher { get; set; }
        public DateTime? PublishDate { get; set; }
        public string? Language { get; set; }
        public string? Series { get; set; }
        public decimal? SeriesPosition { get; set; }
        public byte[]? CoverArt { get; set; }
        public string? CoverArtUrl { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }
}