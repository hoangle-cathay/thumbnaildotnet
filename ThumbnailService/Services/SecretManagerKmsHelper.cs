using Google.Cloud.Kms.V1;
using Google.Cloud.SecretManager.V1;
using Google.Protobuf;
using System;

namespace ThumbnailService.Services
{
    public static class SecretManagerKmsHelper
    {
        public static (byte[] key, byte[] iv) GetAesKeyAndIvFromSecretManager(
            string projectId, string secretId, string kmsKeyResource)
        {
            try
            {
                var secretClient = SecretManagerServiceClient.Create();
                var secretName = new SecretVersionName(projectId, secretId, "latest");
                var secret = secretClient.AccessSecretVersion(secretName);
                Console.WriteLine($"Secret '{secretId}' accessed successfully.");

                string base64Cipher = secret.Payload.Data.ToStringUtf8();
                byte[] cipherBytes = Convert.FromBase64String(base64Cipher);

                var kmsClient = KeyManagementServiceClient.Create();
                var decryptResponse = kmsClient.Decrypt(kmsKeyResource, ByteString.CopyFrom(cipherBytes));
                byte[] plaintext = decryptResponse.Plaintext.ToByteArray();
                Console.WriteLine($"Decrypted plaintext length: {plaintext.Length}");

                if (plaintext.Length < 48)
                    throw new Exception("Decrypted secret too short for AES-256 key and IV.");

                byte[] key = new byte[32];
                byte[] iv = new byte[16];
                Array.Copy(plaintext, 0, key, 0, 32);
                Array.Copy(plaintext, 32, iv, 0, 16);

                return (key, iv);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to load AES key/iv from Secret Manager/KMS.", ex);
            }
        }
    }
}
