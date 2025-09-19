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
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.AspNetCore;

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
        public async Task<IActionResult> HandleGcsEvent()
        {
            try
            {
                // Parse CloudEvent từ HTTP request
                CloudEvent cloudEvent;
                try
                {
                    cloudEvent = await HttpContext.Request.ReadCloudEventAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed reading CloudEvent");
                    return BadRequest("Invalid CloudEvent format");
                }

                _logger.LogInformation("==== EventArc CloudEvent received ====");
                _logger.LogInformation("Event Type: {type}", cloudEvent.Type);
                _logger.LogInformation("Event Source: {source}", cloudEvent.Source);
                _logger.LogInformation("Event Data Content Type: {ctype}", cloudEvent.DataContentType);

                // Parse bucket & object name từ CloudEvent.Data
                if (cloudEvent.Data is not JObject dataObj)
                {
                    _logger.LogWarning("CloudEvent.Data is not a JObject, type={type}", cloudEvent.Data?.GetType().FullName);
                    return BadRequest("Unexpected event data format");
                }

                var bucket = dataObj["bucket"]?.ToString();
                var name = dataObj["name"]?.ToString();
                _logger.LogInformation("Parsed bucket={bucket}, name={name}", bucket, name);

                if (string.IsNullOrEmpty(bucket) || string.IsNullOrEmpty(name))
                {
                    _logger.LogWarning("Missing bucket or object name in CloudEvent");
                    return BadRequest("Missing bucket or name");
                }

                if (bucket != _originalsBucket)
                {
                    _logger.LogWarning("Event for wrong bucket: {bucket}", bucket);
                    return BadRequest("Wrong bucket");
                }

                // Find DB record
                var image = await _db.ImageUploads.FirstOrDefaultAsync(i => i.OriginalGcsPath == name);
                if (image == null)
                {
                    _logger.LogWarning("No DB record found for object: {name}", name);
                    return NotFound("DB record not found");
                }

                var stopwatch = Stopwatch.StartNew();

                // Download original image
                await using var originalStream = new MemoryStream();
                await _storage.DownloadObjectAsync(_originalsBucket, name, originalStream);
                originalStream.Position = 0;
                _logger.LogInformation("Downloaded original image {name} ({size} bytes)", name, originalStream.Length);

                // Detect format & load image
                IImageFormat format = Image.DetectFormat(originalStream);
                originalStream.Position = 0;
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

                // Save thumbnail to stream
                await using var thumbStream = new MemoryStream();
                await imageSharp.SaveAsync(thumbStream, encoder);
                thumbStream.Position = 0;

                var thumbObjectName = $"thumbnails/{image.UserId}/{image.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}.{ext}";

                // Upload thumbnail with retry
                const int maxRetries = 3;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        thumbStream.Position = 0;
                        await _storage.UploadAsync(_thumbnailsBucket, thumbObjectName, thumbStream, format.DefaultMimeType);
                        _logger.LogInformation("Uploaded thumbnail {thumbObjectName} to bucket {bucket}", thumbObjectName, _thumbnailsBucket);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Upload attempt {attempt} failed for {thumbObjectName}", attempt, thumbObjectName);
                        if (attempt == maxRetries) throw;
                        await Task.Delay(1000 * attempt);
                    }
                }

                // Update DB
                image.ThumbnailGcsPath = thumbObjectName;
                image.ThumbnailStatus = ThumbnailStatus.Completed;
                await _db.SaveChangesAsync();

                stopwatch.Stop();
                _logger.LogInformation("Thumbnail processing completed for image {id}: original {originalSize} bytes -> thumbnail {thumbSize} bytes in {ms}ms",
                    image.Id, originalStream.Length, thumbStream.Length, stopwatch.ElapsedMilliseconds);

                return Ok("Thumbnail processed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing EventArc payload");
                return StatusCode(500, "Thumbnail processing failed");
            }
        }
    }
}
