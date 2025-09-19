using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using ThumbnailService.Models;
using ThumbnailService.Services;
using System.Diagnostics;

namespace ThumbnailService.Controllers
{
    [ApiController]
    [Route("/events/gcs")]
    public class ThumbnailController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IStorageService _storage;
        private readonly string _originalsBucket;
        private readonly string _thumbnailsBucket;
        private readonly ILogger<ThumbnailController> _logger;
        private readonly int _thumbWidth;
        private readonly int _thumbHeight;
        private readonly bool _crop;

        public ThumbnailController(AppDbContext db, IStorageService storage, IConfiguration config, ILogger<ThumbnailController> logger)
        {
            _db = db;
            _storage = storage;
            _originalsBucket = config.GetValue<string>("Gcp:OriginalsBucket") ?? string.Empty;
            _thumbnailsBucket = config.GetValue<string>("Gcp:ThumbnailsBucket") ?? string.Empty;
            _thumbWidth = config.GetValue<int>("Thumbnail:Width", 100);
            _thumbHeight = config.GetValue<int>("Thumbnail:Height", 100);
            _crop = config.GetValue<bool>("Thumbnail:Crop", true);
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> HandleGcsEvent([FromBody] JObject cloudEvent)
        {
            var bucket = cloudEvent["data"]?["bucket"]?.ToString();
            var name = cloudEvent["data"]?["name"]?.ToString();
            if (bucket != _originalsBucket || string.IsNullOrEmpty(name))
            {
                _logger.LogWarning("Event for wrong bucket or missing name: {bucket} {name}", bucket, name);
                return BadRequest();
            }

            var image = await _db.ImageUploads.FirstOrDefaultAsync(i => i.OriginalGcsPath == name);
            if (image == null)
            {
                _logger.LogWarning("No DB record for GCS object: {name}", name);
                return NotFound();
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Download original to MemoryStream
                await using var originalStream = new MemoryStream();
                await _storage.DownloadObjectAsync(_originalsBucket, name, originalStream);
                originalStream.Position = 0;

                // Detect image format
                IImageFormat format = Image.DetectFormat(originalStream);
                originalStream.Position = 0; // reset stream

                // Load image
                using Image imageSharp = Image.Load(originalStream);

                // Resize
                imageSharp.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(_thumbWidth, _thumbHeight),
                    Mode = _crop ? ResizeMode.Crop : ResizeMode.Max
                }));

                // Choose encoder
                IImageEncoder encoder;
                string ext;
                if (format.Name.Equals("JPEG", StringComparison.OrdinalIgnoreCase))
                {
                    encoder = new JpegEncoder { Quality = 90 };
                    ext = "jpg";
                }
                else
                {
                    encoder = new PngEncoder();
                    ext = "png";
                }

                // Save to MemoryStream
                await using var thumbStream = new MemoryStream();
                await imageSharp.SaveAsync(thumbStream, encoder);
                thumbStream.Position = 0;

                var thumbObjectName = $"thumbnails/{image.UserId}/{image.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}.{ext}";

                const int maxRetries = 3;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        thumbStream.Position = 0;
                        await _storage.UploadAsync(_thumbnailsBucket, thumbObjectName, thumbStream, format.DefaultMimeType);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Upload attempt {attempt} failed for {thumbObjectName}", attempt, thumbObjectName);
                        if (attempt == maxRetries) throw;
                        await Task.Delay(1000 * attempt);
                    }
                }

                image.ThumbnailGcsPath = thumbObjectName;
                image.ThumbnailStatus = ThumbnailStatus.Completed;
                await _db.SaveChangesAsync();

                stopwatch.Stop();
                _logger.LogInformation("Thumbnail generated for image {id} ({originalSize} bytes -> {thumbSize} bytes) in {ms}ms",
                    image.Id, originalStream.Length, thumbStream.Length, stopwatch.ElapsedMilliseconds);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing image {name}", name);
                return StatusCode(500, "Thumbnail processing failed");
            }
        }
    }
}
