using Google.Cloud.Kms.V1;
using Google.Cloud.SecretManager.V1;
using Google.Protobuf;
using System;

namespace ThumbnailService.Services
{
    public static class SecretManagerKmsHelper
    {
        public static byte[] EncryptWithKms(string kmsKeyResource, byte[] plaintext)
        {
            var kmsClient = KeyManagementServiceClient.Create();
            var response = kmsClient.Encrypt(kmsKeyResource, ByteString.CopyFrom(plaintext));
            return response.Ciphertext.ToByteArray();
        }

        public static byte[] DecryptWithKms(string kmsKeyResource, byte[] ciphertext)
        {
            var kmsClient = KeyManagementServiceClient.Create();
            var response = kmsClient.Decrypt(kmsKeyResource, ByteString.CopyFrom(ciphertext));
            return response.Plaintext.ToByteArray();
        }
    }
}
