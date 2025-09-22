using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace ThumbnailService.Services
{
    public class KmsEncryptionService : IEncryptionService
    {
        private readonly KeyManagementServiceClient _kmsClient;
        private readonly string _kmsKeyName;

        public KmsEncryptionService(KeyManagementServiceClient kmsClient, IConfiguration config)
        {
            _kmsClient = kmsClient;
            _kmsKeyName = config["Gcp:KmsKeyResource"] ?? throw new ArgumentNullException("Gcp:KmsKeyResource");
        }

        public async Task<string> EncryptAsync(string plaintext)
        {
            var request = new EncryptRequest
            {
                Name = _kmsKeyName,
                Plaintext = ByteString.CopyFromUtf8(plaintext)
            };
            var response = await _kmsClient.EncryptAsync(request);
            return Convert.ToBase64String(response.Ciphertext.ToByteArray());
        }

        public async Task<string> DecryptAsync(string base64Ciphertext)
        {
            var ciphertextBytes = Convert.FromBase64String(base64Ciphertext);
            var request = new DecryptRequest
            {
                Name = _kmsKeyName,
                Ciphertext = ByteString.CopyFrom(ciphertextBytes)
            };
            var response = await _kmsClient.DecryptAsync(request);
            return response.Plaintext.ToStringUtf8();
        }
    }
}
