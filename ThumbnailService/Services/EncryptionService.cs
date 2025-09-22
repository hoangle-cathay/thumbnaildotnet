using System;
using System.Security.Cryptography;
using System.Text;

namespace ThumbnailService.Services
{
    public interface IEncryptionService
    {
        string Encrypt(string plaintext);
        string Decrypt(string ciphertext);

        Task<string> EncryptAsync(string plaintext);
        Task<string> DecryptAsync(string ciphertext);
    }


    public class EncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public EncryptionService(byte[] key, byte[] iv)
        {
            _key = key;
            _iv = iv;
        }


        public string Encrypt(string plaintext)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
            return Convert.ToBase64String(cipherBytes);
        }

        public Task<string> EncryptAsync(string plaintext)
        {
            // Synchronous implementation for compatibility
            return Task.FromResult(Encrypt(plaintext));
        }

        public string Decrypt(string ciphertextBase64)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(ciphertextBase64);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        public Task<string> DecryptAsync(string ciphertextBase64)
        {
            // Synchronous implementation for compatibility
            return Task.FromResult(Decrypt(ciphertextBase64));
        }
    }
}


