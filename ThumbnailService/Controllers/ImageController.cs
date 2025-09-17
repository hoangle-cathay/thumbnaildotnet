using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThumbnailService.Models;
using ThumbnailService.Services;

namespace ThumbnailService.Controllers
{
    //[Authorize]
    public class ImageController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IStorageService _storage;
        private readonly string _thumbnailsBucket;

        public ImageController(AppDbContext db, IStorageService storage, IConfiguration config)
        {
            _db = db;
            _storage = storage;
            _thumbnailsBucket = config.GetValue<string>("Gcp:ThumbnailsBucket") ?? string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            //var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            //var userId = Guid.Parse("11111111-1111-1111-1111-111111111111"); // For local debugging
            // var images = await _db.ImageUploads.Where(i => i.UserId == userId)
            //     .OrderByDescending(i => i.UploadedAtUtc)
            //     .ToListAsync();

            // Test signed URL for thumbnails
            var userId = Guid.Parse("11111111-1111-1111-1111-111111111111"); // For local debugging
            var images = await _db.ImageUploads.Where(i => i.UserId == userId)
                .OrderByDescending(i => i.UploadedAtUtc)
                .ToListAsync();

            Console.WriteLine($"Found {images.Count} images for user {userId}");

            return View(images);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadThumbnail(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var img = await _db.ImageUploads.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);
            if (img == null || string.IsNullOrEmpty(img.ThumbnailGcsPath))
                return NotFound();

            var url = _storage.GetPublicUrl(_thumbnailsBucket, img.ThumbnailGcsPath);
            return Redirect(url);
        }
        
        public static List<ImageUpload> GetDummyImages(Guid userId)
        {
            return new List<ImageUpload>
            {
                new ImageUpload
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OriginalFileName = "cat.png",
                    ContentType = "image/png",
                    FileSizeBytes = 245678,
                    OriginalGcsPath = "originals/user123/cat.png",
                    ThumbnailGcsPath = "thumbnails/user123/cat_thumb.png",
                    ThumbnailStatus = ThumbnailStatus.Completed,
                    UploadedAtUtc = DateTime.UtcNow.AddMinutes(-5)
                },
                new ImageUpload
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OriginalFileName = "dog.jpg",
                    ContentType = "image/jpeg",
                    FileSizeBytes = 367890,
                    OriginalGcsPath = "originals/user123/dog.jpg",
                    ThumbnailGcsPath = "thumbnails/user123/dog_thumb.jpg",
                    ThumbnailStatus = ThumbnailStatus.Completed,
                    UploadedAtUtc = DateTime.UtcNow.AddMinutes(-10)
                },
                new ImageUpload
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OriginalFileName = "sunset.jpeg",
                    ContentType = "image/jpeg",
                    FileSizeBytes = 567123,
                    OriginalGcsPath = "originals/user123/sunset.jpeg",
                    ThumbnailGcsPath = null, // still pending
                    ThumbnailStatus = ThumbnailStatus.Pending,
                    UploadedAtUtc = DateTime.UtcNow.AddMinutes(-30)
                }
            };
        }
    }
}


