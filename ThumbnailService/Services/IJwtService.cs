using System.Security.Claims;
namespace ThumbnailService.Services
{
    public interface IJwtService
    {
        Task<string> GenerateTokenAsync(Guid userId, string username);
        Task<ClaimsPrincipal?> ValidateTokenAsync(string token);
    }
}