namespace ThumbnailService.Services
{
    public interface IEncryptionService
    {
        Task<string> EncryptAsync(string plaintext);
        Task<string> DecryptAsync(string base64Ciphertext);
    }
}
