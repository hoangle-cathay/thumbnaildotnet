using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;

namespace ThumbnailService.Services
{
    public class GoogleStorageService : IStorageService
    {
        private readonly StorageClient _storageClient;

        public GoogleStorageService(StorageClient storageClient)
        {
            _storageClient = storageClient;
        }

        public async Task<string> UploadAsync(string bucket, string objectName, Stream content, string contentType)
        {
            var obj = await _storageClient.UploadObjectAsync(bucket, objectName, contentType, content);
            return obj.MediaLink ?? $"gs://{bucket}/{objectName}";
        }

        public async Task DownloadObjectAsync(string bucket, string objectName, Stream destination)
        {
            await _storageClient.DownloadObjectAsync(bucket, objectName, destination);
        }

        public string GetPublicUrl(string bucket, string objectName)
        {
            return $"https://storage.googleapis.com/{bucket}/{Uri.EscapeDataString(objectName)}";
        }
    }
}
