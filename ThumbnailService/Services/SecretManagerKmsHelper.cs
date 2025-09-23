using Google.Cloud.Kms.V1;
using Google.Cloud.SecretManager.V1;
using Google.Protobuf;
using System;

namespace ThumbnailService.Services
{
    public static class SecretManagerKmsHelper
    {

        public static byte[] EncryptWithKms(string kmsKeyResource, byte[] plaintext, Microsoft.Extensions.Logging.ILogger logger = null)
        {
            logger?.LogInformation("Encrypting data with KMS key: {KmsKey}", kmsKeyResource);
            var kmsClient = KeyManagementServiceClient.Create();
            var response = kmsClient.Encrypt(kmsKeyResource, ByteString.CopyFrom(plaintext));
            logger?.LogInformation("Encryption complete. Returning ciphertext.");
            return response.Ciphertext.ToByteArray();
        }

        public static byte[] DecryptWithKms(string kmsKeyResource, byte[] ciphertext, Microsoft.Extensions.Logging.ILogger logger = null)
        {
            logger?.LogInformation("Decrypting data with KMS key: {KmsKey}", kmsKeyResource);
            var kmsClient = KeyManagementServiceClient.Create();
            var response = kmsClient.Decrypt(kmsKeyResource, ByteString.CopyFrom(ciphertext));
            logger?.LogInformation("Decryption complete. Returning plaintext.");
            return response.Plaintext.ToByteArray();
        }
    }
}
