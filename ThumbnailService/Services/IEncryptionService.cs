namespace ThumbnailService.Services
{
    public interface IEncryptionService
    {
        string Encrypt(string plaintext);
        string Decrypt(string ciphertextBase64);
    }
}
