using System.ComponentModel.DataAnnotations;

namespace Listenarr.Domain.Models
{
    public class Tag
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Label { get; set; } = string.Empty;
    }
}
