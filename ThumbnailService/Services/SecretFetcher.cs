using Google.Cloud.SecretManager.V1;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using System;

namespace ThumbnailService.Services
{

    public class SecretFetcher
    {
        private readonly SecretManagerServiceClient _secretClient;
        private readonly KeyManagementServiceClient _kmsClient;
        private readonly Microsoft.Extensions.Logging.ILogger<SecretFetcher> _logger;

        public SecretFetcher(Microsoft.Extensions.Logging.ILogger<SecretFetcher> logger)
        {
            _secretClient = SecretManagerServiceClient.Create();
            _kmsClient = KeyManagementServiceClient.Create();
            _logger = logger;
        }

        public string GetDecryptedPassword()
        {
            _logger.LogInformation("1. Fetching encrypted DB password (Base64 ciphertext) from Secret Manager...");
            var secretName = new SecretVersionName("hoangassignment", "db-password-enc", "latest");
            var secret = _secretClient.AccessSecretVersion(secretName);

            byte[] cipherBytes;

            try
            {
                // Case 1: Secret payload là raw binary (đúng chuẩn khi add từ file)
                cipherBytes = secret.Payload.Data.ToByteArray();
                _logger.LogInformation("2. Fetched secret as raw binary.");
            }
            catch
            {
                // Case 2: Nếu payload là string base64 (lỡ upload nhầm)
                string base64Cipher = secret.Payload.Data.ToStringUtf8();
                _logger.LogInformation("Fetched secret as base64 string, decoding...");
                cipherBytes = Convert.FromBase64String(base64Cipher);
            }

            _logger.LogInformation("3. Calling KMS to decrypt DB password...");
            var keyName = new CryptoKeyName("hoangassignment", "asia-southeast1", "thumbnail-keyring", "thumbnail-key");
            var decryptResponse = _kmsClient.Decrypt(keyName, ByteString.CopyFrom(cipherBytes));

            _logger.LogInformation("4. Decryption complete. Returning plaintext password.");
            return decryptResponse.Plaintext.ToStringUtf8();
        }
    }
}
