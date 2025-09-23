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
        private readonly Microsoft.Extensions.Logging.ILogger<KmsEncryptionService> _logger;

        public KmsEncryptionService(KeyManagementServiceClient kmsClient, IConfiguration config, Microsoft.Extensions.Logging.ILogger<KmsEncryptionService> logger)
        {
            _kmsClient = kmsClient;
            _kmsKeyName = config["Gcp:KmsKeyResource"] ?? throw new ArgumentNullException("Gcp:KmsKeyResource");
            _logger = logger;
        }


        public async Task<string> EncryptAsync(string plaintext)
        {
            _logger.LogInformation("Encrypting data with KMS key: {KmsKey}", _kmsKeyName);
            var request = new EncryptRequest
            {
                Name = _kmsKeyName,
                Plaintext = ByteString.CopyFromUtf8(plaintext)
            };
            var response = await _kmsClient.EncryptAsync(request);
            _logger.LogInformation("Encryption complete. Returning base64 ciphertext.");
            return Convert.ToBase64String(response.Ciphertext.ToByteArray());
        }

        public async Task<string> DecryptAsync(string base64Ciphertext)
        {
            _logger.LogInformation("Decrypting data with KMS key: {KmsKey}", _kmsKeyName);
            var ciphertextBytes = Convert.FromBase64String(base64Ciphertext);
            var request = new DecryptRequest
            {
                Name = _kmsKeyName,
                Ciphertext = ByteString.CopyFrom(ciphertextBytes)
            };
            var response = await _kmsClient.DecryptAsync(request);
            _logger.LogInformation("Decryption complete. Returning plaintext.");
            return response.Plaintext.ToStringUtf8();
        }
    }
}
