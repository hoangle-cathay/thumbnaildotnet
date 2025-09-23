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

        public SecretFetcher()
        {
            _secretClient = SecretManagerServiceClient.Create();
            _kmsClient = KeyManagementServiceClient.Create();
        }

        public string GetDecryptedPassword()
        {
            // 1. Fetch secret (Base64 ciphertext) from Secret Manager
            var secretName = new SecretVersionName("hoangassignment", "db-password-enc", "latest");
            var secret = _secretClient.AccessSecretVersion(secretName);

            byte[] cipherBytes;

            try
            {
                // Case 1: Secret payload là raw binary (đúng chuẩn khi add từ file)
                cipherBytes = secret.Payload.Data.ToByteArray();
            }
            catch
            {
                // Case 2: Nếu payload là string base64 (lỡ upload nhầm)
                string base64Cipher = secret.Payload.Data.ToStringUtf8();
                // 2. Decode from Base64 → bytes
                cipherBytes = Convert.FromBase64String(base64Cipher);
            }

            // 3. Call KMS to decrypt
            var keyName = new CryptoKeyName("hoangassignment", "asia-southeast1", "thumbnail-keyring", "thumbnail-key");
            var decryptResponse = _kmsClient.Decrypt(keyName, ByteString.CopyFrom(cipherBytes));

            // 4. Convert plaintext back to string
            return decryptResponse.Plaintext.ToStringUtf8();
        }
    }
}
