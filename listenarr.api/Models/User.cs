using System.ComponentModel.DataAnnotations;

namespace Listenarr.Api.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        // Stored as base64(salt) + ':' + base64(hash)
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public string? Email { get; set; }
        public bool IsAdmin { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
