using System;
using System.ComponentModel.DataAnnotations;

namespace ThumbnailService.Models
{
    public enum ThumbnailStatus
    {
        Pending = 0,
        Completed = 1
    }

    public class ImageUpload
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(256)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        [Required]
        [MaxLength(512)]
        public string OriginalGcsPath { get; set; } = string.Empty;

        [MaxLength(512)]
        public string? ThumbnailGcsPath { get; set; }

        public ThumbnailStatus ThumbnailStatus { get; set; } = ThumbnailStatus.Pending;

        public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    }
}


