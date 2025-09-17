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
    //[Authorize]
    public class UploadController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IStorageService _storage;
        private readonly IThumbnailService _thumbnail;

        private readonly string _originalsBucket;
        private readonly string _thumbnailsBucket;

        public UploadController(AppDbContext db, IStorageService storage, IThumbnailService thumbnail, IConfiguration config)
        {
            _db = db;
            _storage = storage;
            _thumbnail = thumbnail;
            _originalsBucket = config.GetValue<string>("Gcp:OriginalsBucket") ?? string.Empty;
            _thumbnailsBucket = config.GetValue<string>("Gcp:ThumbnailsBucket") ?? string.Empty;
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

            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
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

            // Generate thumbnail synchronously after upload
            await using var thumbInputStream = file.OpenReadStream();
            var thumbBytes = await _thumbnail.GenerateFilledThumbnailAsync(thumbInputStream);
            var thumbObjectName = $"thumbnails/{userId}/{image.Id}.png";
            await using var thumbStream = new MemoryStream(thumbBytes);
            await _storage.UploadAsync(_thumbnailsBucket, thumbObjectName, thumbStream, "image/png");

            image.ThumbnailGcsPath = thumbObjectName;
            image.ThumbnailStatus = ThumbnailStatus.Completed;
            await _db.SaveChangesAsync();

            TempData["Message"] = "Upload and thumbnail complete";
            return RedirectToAction("List", "Image");
        }
    }
}


