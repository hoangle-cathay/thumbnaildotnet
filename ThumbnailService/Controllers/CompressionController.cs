using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThumbnailService.Models;
using ThumbnailService.Services;

namespace ThumbnailService.Controllers
{
    // Eventarc target endpoint to compress originals periodically.
    [ApiController]
    [Route("/jobs/compress")]
    public class CompressionController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IStorageService _storage;
        private readonly string _originalsBucket;
        private readonly string _thumbnailsBucket;
        private readonly string _compressedPrefix;

        public CompressionController(AppDbContext db, IStorageService storage, IConfiguration config)
        {
            _db = db;
            _storage = storage;
            _originalsBucket = config.GetValue<string>("Gcp:OriginalsBucket") ?? string.Empty;
            _thumbnailsBucket = config.GetValue<string>("Gcp:ThumbnailsBucket") ?? string.Empty;
            _compressedPrefix = config.GetValue<string>("Gcp:CompressedPrefix") ?? "compressed/";
        }

        [HttpPost]
        public async Task<IActionResult> Run()
        {
            var images = await _db.ImageUploads.OrderByDescending(i => i.UploadedAtUtc).Take(50).ToListAsync();
            foreach (var img in images)
            {
                // Place a tiny stub zip for demonstration. Real implementation would download, compress, reupload.
                var objectName = $"{_compressedPrefix}{img.Id}.zip";
                await using var mem = new MemoryStream();
                using (var zip = new ZipArchive(mem, ZipArchiveMode.Create, true))
                {
                    var entry = zip.CreateEntry(img.OriginalFileName);
                    await using var entryStream = entry.Open();
                    await entryStream.WriteAsync(new byte[] { 0x50, 0x4B });
                }
                mem.Position = 0;
                await _storage.UploadAsync(_thumbnailsBucket, objectName, mem, "application/zip");
            }
            return Ok(new { processed = images.Count });
        }
    }
}


