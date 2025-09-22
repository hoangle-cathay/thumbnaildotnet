using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ThumbnailService.Services;

namespace ThumbnailService.Middleware
{
    public class JwtAuthMiddleware
    {
        private readonly RequestDelegate _next;

        public JwtAuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IJwtService jwtService)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length);
                var principal = await jwtService.ValidateTokenAsync(token);
                if (principal != null)
                {
                    context.User = principal;
                }
                else
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Invalid or expired token");
                    return;
                }
            }
            else if (context.Request.Path.StartsWithSegments("/Account"))
            {
                // Allow unauthenticated for /Account endpoints
            }
            else
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Missing or invalid Authorization header");
                return;
            }
            await _next(context);
        }
    }
}