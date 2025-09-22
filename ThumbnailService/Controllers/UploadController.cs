using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThumbnailService.Models;
using ThumbnailService.Services;

namespace ThumbnailService.Controllers
{
    [Authorize]
    public class UploadController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IStorageService _storage;
    private readonly IThumbnailService _thumbnail;
    private readonly PubSubService _pubSub;

        private readonly string _originalsBucket;
        private readonly string _thumbnailsBucket;

    public UploadController(AppDbContext db, IStorageService storage, IThumbnailService thumbnail, PubSubService pubSub, IConfiguration config)
        {
            _db = db;
            _storage = storage;
            _thumbnail = thumbnail;
            _originalsBucket = config.GetValue<string>("Gcp:OriginalsBucket") ?? string.Empty;
            _thumbnailsBucket = config.GetValue<string>("Gcp:ThumbnailsBucket") ?? string.Empty;
            _pubSub = pubSub;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpPost]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Upload()
        {
            if (Request.Form.Files.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "No file uploaded");
                return View("Index");
            }

            var file = Request.Form.Files[0];
            var contentType = file.ContentType.ToLowerInvariant();
            if (contentType != "image/png" && contentType != "image/jpeg")
            {
                ModelState.AddModelError(string.Empty, "Only PNG or JPG allowed");
                return View("Index");
            }

            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                ModelState.AddModelError(string.Empty, "User not authenticated");
                return View("Index");
            }
            var originalObjectName = $"originals/{userId}/{Guid.NewGuid()}_{file.FileName}";
            await using var stream = file.OpenReadStream();
            await _storage.UploadAsync(_originalsBucket, originalObjectName, stream, contentType);

            var image = new ImageUpload
            {
                UserId = userId,
                OriginalFileName = file.FileName,
                ContentType = contentType,
                FileSizeBytes = file.Length,
                OriginalGcsPath = originalObjectName,
                ThumbnailStatus = ThumbnailStatus.Pending
            };
            _db.ImageUploads.Add(image);
            await _db.SaveChangesAsync();

            // Publish event for async thumbnail creation (Pub/Sub)
            //await PublishThumbnailJobAsync(image.Id, userId, originalObjectName);

            TempData["Message"] = "Upload complete. Thumbnail will be generated soon.";
            return RedirectToAction("List", "Image");
        }

        // This method should publish a message to Pub/Sub for async thumbnail creation
        private async Task PublishThumbnailJobAsync(Guid imageId, Guid userId, string gcsPath)
        {
            var job = new ThumbnailService.Services.ThumbnailJob
            {
                ImageId = imageId,
                UserId = userId,
                GcsPath = gcsPath
            };
            await _pubSub.PublishAsync(job);
        }
    }
}


