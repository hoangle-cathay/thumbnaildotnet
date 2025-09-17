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
            // If bucket is "local-xxx" save at Local instead of GCS
            if (bucket.StartsWith("local-"))
            {
                var basePath = Path.Combine(Directory.GetCurrentDirectory(), "local_storage", bucket);
                Directory.CreateDirectory(basePath);

                var safeFileName = objectName.Replace("/", "_");
                var filePath = Path.Combine(basePath, safeFileName);

                using (var fileStream = File.Create(filePath))
                {
                    await content.CopyToAsync(fileStream);
                }

                Console.WriteLine($"[LOCAL STORAGE] Saved file: {filePath}");
                return $"file://{filePath}";
            }

            try
            {
                var obj = await _storageClient.UploadObjectAsync(bucket, objectName, contentType, content);
                return obj.MediaLink ?? $"gs://{bucket}/{objectName}";
            }
            catch (Exception ex)
            {
                // Log or rethrow as needed
                throw new Exception($"Failed to upload to GCS: {ex.Message}", ex);
            }
        }

        public string GetPublicUrl(string bucket, string objectName)
        {
            return $"https://storage.googleapis.com/{bucket}/{Uri.EscapeDataString(objectName)}";
        }

        public string GetSignedUrl(string bucket, string objectName, int expiryMinutes = 60)
        {
            UrlSigner signer = UrlSigner.FromServiceAccountPath(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"));
            var url = signer.Sign(bucket, objectName, TimeSpan.FromMinutes(expiryMinutes), HttpMethod.Get);
            return url;
        }

        public async Task<IList<string>> ListUserObjectsAsync(string bucket, string userIdPrefix)
        {
            var results = new List<string>();
            try
            {
                await foreach (var obj in _storageClient.ListObjectsAsync(bucket, userIdPrefix))
                {
                    results.Add(obj.Name);
                }
            }
            catch (Exception ex)
            {
                // Log or rethrow as needed
                throw new Exception($"Failed to list objects for user: {ex.Message}", ex);
            }
            return results;
        }

        public static StorageClient CreateClientFromEnvironment()
        {
            // Prefer ADC. If GOOGLE_APPLICATION_CREDENTIALS points to a file, ADC will use it.
            GoogleCredential credential = GoogleCredential.GetApplicationDefault();
            return StorageClient.Create(credential);
        }
    }
}


