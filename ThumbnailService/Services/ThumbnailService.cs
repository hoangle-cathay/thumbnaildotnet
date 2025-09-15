using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ThumbnailService.Services
{
    public interface IThumbnailService
    {
        Task<byte[]> GenerateFilledThumbnailAsync(Stream input, int width = 100, int height = 100);
    }

    public class ThumbnailServiceImpl : IThumbnailService
    {
        public async Task<byte[]> GenerateFilledThumbnailAsync(Stream input, int width = 100, int height = 100)
        {
            input.Position = 0;
            using var image = await Image.LoadAsync<Rgba32>(input);

            var options = new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Crop,
                Sampler = KnownResamplers.Bicubic
            };

            image.Mutate(x => x.Resize(options));

            using var outStream = new MemoryStream();
            await image.SaveAsPngAsync(outStream, new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.BestCompression
            });
            return outStream.ToArray();
        }
    }
}


