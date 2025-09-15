using System;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
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

        public async Task<string> UploadAsync(string bucket, string objectName, System.IO.Stream content, string contentType)
        {
            var obj = await _storageClient.UploadObjectAsync(bucket, objectName, contentType, content);
            return obj.MediaLink ?? $"gs://{bucket}/{objectName}";
        }

        public string GetPublicUrl(string bucket, string objectName)
        {
            return $"https://storage.googleapis.com/{bucket}/{Uri.EscapeDataString(objectName)}";
        }

        public static StorageClient CreateClientFromEnvironment()
        {
            // Prefer ADC. If GOOGLE_APPLICATION_CREDENTIALS points to a file, ADC will use it.
            GoogleCredential credential = GoogleCredential.GetApplicationDefault();
            return StorageClient.Create(credential);
        }
    }
}


