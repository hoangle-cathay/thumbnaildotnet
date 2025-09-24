using Google.Cloud.SecretManager.V1;
using Google.Cloud.Kms.V1;
using Google.Protobuf;

namespace ThumbnailService.Services
{
    public class SecretFetcher
    {
        private readonly SecretManagerServiceClient _secretClient;
        private readonly KeyManagementServiceClient _kmsClient;
        private readonly ILogger<SecretFetcher> _logger;

        public SecretFetcher(ILogger<SecretFetcher> logger)
        {
            _secretClient = SecretManagerServiceClient.Create();
            _kmsClient = KeyManagementServiceClient.Create();
            _logger = logger;
        }

        public string GetDecryptedPassword(bool logPlaintext = false)
        {
            try
            {
                _logger.LogInformation("Fetching encrypted DB password from Secret Manager...");
                var secretName = new SecretVersionName("hoangassignment", "db-password-enc", "latest");
                var secret = _secretClient.AccessSecretVersion(secretName);

                byte[] cipherBytes;
                try
                {
                    cipherBytes = secret.Payload.Data.ToByteArray();
                    _logger.LogInformation($"Fetched secret as raw binary ({cipherBytes.Length} bytes).");
                }
                catch
                {
                    string base64Cipher = secret.Payload.Data.ToStringUtf8();
                    _logger.LogInformation($"Fetched secret as base64 string ({base64Cipher.Length} chars), decoding...");
                    cipherBytes = Convert.FromBase64String(base64Cipher);
                }

                _logger.LogInformation("Calling KMS to decrypt DB password...");
                var keyName = new CryptoKeyName("hoangassignment", "asia-southeast1", "thumbnail-keyring", "thumbnail-key");
                var decryptResponse = _kmsClient.Decrypt(keyName, ByteString.CopyFrom(cipherBytes));

                var plaintext = decryptResponse.Plaintext.ToStringUtf8();
                _logger.LogInformation($"Decryption complete. Ciphertext length: {cipherBytes.Length}, Plaintext length: {plaintext.Length}.");

                if (logPlaintext)
                {
                    _logger.LogWarning($"Decrypted DB password (debug only): {plaintext}");
                }

                return plaintext;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch/decrypt DB password from Secret Manager or KMS.");
                throw;
            }
        }
    }
}
