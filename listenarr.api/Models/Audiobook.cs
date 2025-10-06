using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Listenarr.Api.Models
{
    public class Audiobook
    {
        [Key]
        public int Id { get; set; }
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
        public string? Asin { get; set; }
        public string? Publisher { get; set; }
        public string? Language { get; set; }
        public int? Runtime { get; set; }
        public string? Version { get; set; }
        public bool Explicit { get; set; }
        public bool Abridged { get; set; }
    }
}
