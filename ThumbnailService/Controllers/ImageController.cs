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

        public ImageController(AppDbContext db, IStorageService storage, IConfiguration config)
        {
            _db = db;
            _storage = storage;
            _thumbnailsBucket = config.GetValue<string>("Gcp:ThumbnailsBucket") ?? string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var images = await _db.ImageUploads.Where(i => i.UserId == userId)
                .OrderByDescending(i => i.UploadedAtUtc)
                .ToListAsync();
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
    }
}


