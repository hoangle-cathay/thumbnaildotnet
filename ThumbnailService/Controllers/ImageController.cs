using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThumbnailService.Models;
using ThumbnailService.Services;

namespace ThumbnailService.Controllers
{
    [Authorize]
    public class ImageController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IStorageService _storage;
    private readonly string _thumbnailsBucket;
    private readonly ILogger<ImageController> _logger;

        public ImageController(AppDbContext db, IStorageService storage, IConfiguration config, ILogger<ImageController> logger)
        {
            _db = db;
            _storage = storage;
            _thumbnailsBucket = config.GetValue<string>("Gcp:ThumbnailsBucket") ?? string.Empty;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();
            _logger.LogInformation("[List] Fetching images for user {UserId}", userId);
            var images = await _db.ImageUploads.Where(i => i.UserId == userId)
                .OrderByDescending(i => i.UploadedAtUtc)
                .ToListAsync();
            _logger.LogInformation("[List] Found {Count} images for user {UserId}", images.Count, userId);
            foreach (var img in images)
            {
                _logger.LogDebug("[List] Image: Id={Id}, File={File}, Thumb={Thumb}, Status={Status}, Uploaded={Uploaded}",
                    img.Id, img.OriginalFileName, img.ThumbnailGcsPath, img.ThumbnailStatus, img.UploadedAtUtc);
            }
            return View(images);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadThumbnail(Guid id)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();
            _logger.LogInformation("[DownloadThumbnail] User {UserId} requested thumbnail for image {ImageId}", userId, id);
            var img = await _db.ImageUploads.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);
            if (img == null)
            {
                _logger.LogWarning("[DownloadThumbnail] No image found for Id={ImageId} and UserId={UserId}", id, userId);
                return NotFound();
            }
            if (string.IsNullOrEmpty(img.ThumbnailGcsPath))
            {
                _logger.LogWarning("[DownloadThumbnail] Image {ImageId} for User {UserId} has no thumbnail path", id, userId);
                return NotFound();
            }
            var url = _storage.GetPublicUrl(_thumbnailsBucket, img.ThumbnailGcsPath);
            _logger.LogInformation("[DownloadThumbnail] Redirecting to thumbnail URL: {Url}", url);
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


