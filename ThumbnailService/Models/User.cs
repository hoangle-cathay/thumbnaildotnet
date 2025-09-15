using System;
using System.ComponentModel.DataAnnotations;

namespace ThumbnailService.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(128)]
        public string Email { get; set; } = string.Empty;

        // Encrypted password (AES/RSA). Not storing plaintext or hash per requirements.
        [Required]
        public string EncryptedPassword { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}


