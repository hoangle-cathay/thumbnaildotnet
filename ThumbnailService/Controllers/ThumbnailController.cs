using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using ThumbnailService.Models;
using ThumbnailService.Services;
using System.Diagnostics;
using System.Text.Json;

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

        public ThumbnailController(
            AppDbContext db,
            IStorageService storage,
            IConfiguration config,
            ILogger<ThumbnailController> logger)
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
            _logger.LogInformation("Received GCS event: HandleGcsEvent called.");

            try
            {
                // ✅ đọc raw body
                string rawBody;
                using (var reader = new StreamReader(HttpContext.Request.Body))
                {
                    rawBody = await reader.ReadToEndAsync();
                }
                _logger.LogInformation("Full Event Payload: {payload}", rawBody);

                // ✅ parse JSON từ raw body
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                // bắt buộc phải có bucket + name
                if (!root.TryGetProperty("bucket", out var bucketProp) ||
                    !root.TryGetProperty("name", out var nameProp))
                {
                    _logger.LogWarning("Missing bucket or name in payload: {payload}", rawBody);
                    return BadRequest("Invalid event payload: missing bucket or name");
                }

                var bucket = bucketProp.GetString();
                var name = nameProp.GetString();
                _logger.LogInformation("Parsed bucket={bucket}, name={name}", bucket, name);

                if (string.IsNullOrEmpty(bucket) || string.IsNullOrEmpty(name))
                {
                    return BadRequest("Invalid payload: empty bucket or name");
                }

                // ✅ chỉ xử lý nếu là bucket gốc
                if (!string.Equals(bucket, _originalsBucket, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Ignored event for unrelated bucket: {bucket}", bucket);
                    return Ok("Ignored event");
                }

                _logger.LogInformation("Looking for DB record with path: {Path}", name);
                _logger.LogInformation("Looking up record: DB.OriginalGcsPath vs GCS name => {DbExample} vs {GcsName}",
                    await _db.ImageUploads.Select(i => i.OriginalGcsPath).FirstOrDefaultAsync(),
                    name);
                var allPaths = await _db.ImageUploads.Select(i => i.OriginalGcsPath).ToListAsync();
                    _logger.LogInformation("DB has paths: {paths}", string.Join(", ", allPaths));

                // ✅ tìm bản ghi DB
                var image = await _db.ImageUploads.FirstOrDefaultAsync(i => EF.Functions.ILike(i.OriginalGcsPath, name));

                if (image == null)
                {
                    _logger.LogWarning("No DB record found for object: {name}", name);
                    return Ok("No matching DB record");
                }

                var stopwatch = Stopwatch.StartNew();

                // tải file gốc
                await using var originalStream = new MemoryStream();
                await _storage.DownloadObjectAsync(_originalsBucket, name, originalStream);
                originalStream.Position = 0;

                // resize thumbnail
                using var imageSharp = Image.Load(originalStream);
                imageSharp.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(_thumbWidth, _thumbHeight),
                    Mode = _crop ? ResizeMode.Crop : ResizeMode.Max
                }));

                await using var thumbStream = new MemoryStream();
                await imageSharp.SaveAsync(thumbStream, new PngEncoder());
                thumbStream.Position = 0;

                var thumbObjectName =
                    $"thumbnails/{image.UserId}/{image.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}.png";

                // retry upload
                const int maxRetries = 3;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        thumbStream.Position = 0;
                        await _storage.UploadAsync(_thumbnailsBucket, thumbObjectName, thumbStream, "image/png");
                        _logger.LogInformation("Uploaded thumbnail {thumbObjectName} to {bucket}",
                            thumbObjectName, _thumbnailsBucket);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Upload attempt {attempt} failed for {thumbObjectName}",
                            attempt, thumbObjectName);
                        if (attempt == maxRetries) throw;
                        await Task.Delay(1000 * attempt);
                    }
                }

                // cập nhật DB
                image.ThumbnailGcsPath = thumbObjectName;
                image.ThumbnailStatus = ThumbnailStatus.Completed;
                await _db.SaveChangesAsync();

                stopwatch.Stop();
                _logger.LogInformation("Thumbnail done for image {id}: {ms}ms",
                    image.Id, stopwatch.ElapsedMilliseconds);

                return Ok("Thumbnail processed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Eventarc payload");
                return StatusCode(500, "Thumbnail processing failed");
            }
        }
    }
}
