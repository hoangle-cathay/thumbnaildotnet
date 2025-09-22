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
            else if (
                context.Request.Path.StartsWithSegments("/Account") ||
                context.Request.Path == "/" ||
                context.Request.Path.StartsWithSegments("/Home") ||
                context.Request.Path.StartsWithSegments("/favicon.ico") ||
                context.Request.Path.StartsWithSegments("/css") ||
                context.Request.Path.StartsWithSegments("/js") ||
                context.Request.Path.StartsWithSegments("/lib")
            )
            {
                // Allow unauthenticated for public endpoints
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