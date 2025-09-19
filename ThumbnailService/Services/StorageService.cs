using System;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;

namespace ThumbnailService.Services
{
    public interface IStorageService
    {
        Task<string> UploadAsync(string bucket, string objectName, System.IO.Stream content, string contentType);
        string GetPublicUrl(string bucket, string objectName);
        Task DownloadObjectAsync(string bucket, string objectName, System.IO.Stream destination);
    }

    public class StorageService : IStorageService
    {
        private readonly StorageClient _storageClient;

        public StorageService(StorageClient storageClient)
        {
            _storageClient = storageClient;
        }

        public async Task<string> UploadAsync(string bucket, string objectName, System.IO.Stream content, string contentType)
        {
            var obj = await _storageClient.UploadObjectAsync(bucket, objectName, contentType, content);
            return obj.MediaLink ?? $"gs://{bucket}/{objectName}";
        }

        public string GetPublicUrl(string bucket, string objectName)
        {
            return $"https://storage.googleapis.com/{bucket}/{Uri.EscapeDataString(objectName)}";
        }

        public async Task DownloadObjectAsync(string bucket, string objectName, System.IO.Stream destination)
        {
            await _storageClient.DownloadObjectAsync(bucket, objectName, destination);
        }
    }
}


