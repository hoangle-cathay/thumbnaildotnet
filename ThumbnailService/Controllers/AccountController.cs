using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThumbnailService.Models;
using ThumbnailService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace ThumbnailService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IEncryptionService _encryption;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            AppDbContext db, 
            IEncryptionService encryption, 
            ILogger<AccountController> logger)
        {
            _db = db;
            _encryption = encryption;
            _logger = logger;
        }

        [HttpGet("healthcheck")]
        [AllowAnonymous]
        public async Task<IActionResult> HealthCheck()
        {
            var count = await _db.Users.CountAsync();
            return Ok(new { status = "ok", userCount = count });
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            _logger.LogInformation("Registration attempt for email: {Email}", request.Email);
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("Registration failed: missing email or password");
                return BadRequest(new { error = "Email and password required" });
            }

            bool exists = false;
            try
            {
                exists = await _db.Users.AnyAsync(u => u.Email == request.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError("Fail to connect to DB: {Message}", ex.Message);
                return StatusCode(500, new { error = "Database connection failed" });
            }

            if (exists)
            {
                _logger.LogWarning("Registration failed: email {Email} already registered", request.Email);
                return BadRequest(new { error = "Email already registered" });
            }

            // Encrypt password
            string encryptedPassword;
            try
            {
                _logger.LogInformation("Encrypting password for {Email}", request.Email);
                encryptedPassword = await _encryption.EncryptAsync(request.Password);
                _logger.LogInformation("Password encrypted for {Email}", request.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password encryption for {Email}", request.Email);
                return StatusCode(500, new { error = "Internal error, please try again later" });
            }

            var user = new User
            {
                Email = request.Email,
                EncryptedPassword = encryptedPassword
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("User {Email} registered successfully with Id {UserId}", request.Email, user.Id);
            return Ok(new { message = "Registration successful", userId = user.Id });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login attempt for email: {Email}", request.Email);
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                _logger.LogWarning("Login failed: user {Email} not found", request.Email);
                return BadRequest(new { error = "Invalid credentials" });
            }

            // Decrypt password
            _logger.LogInformation("Decrypting password for {Email}", request.Email);
            var decryptedPassword = await _encryption.DecryptAsync(user.EncryptedPassword);
            if (decryptedPassword != request.Password)
            {
                _logger.LogWarning("Login failed: invalid password for {Email}", request.Email);
                return BadRequest(new { error = "Invalid credentials" });
            }

            // Sign in with cookie auth
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            _logger.LogInformation("User {Email} logged in successfully (Id: {UserId})", request.Email, user.Id);
            
            return Ok(new { message = "Login successful", userId = user.Id, email = user.Email });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("User logged out");
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { message = "Logout successful" });
        }

        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return Unauthorized();
            
            return Ok(new { 
                userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                email = User.FindFirst(ClaimTypes.Name)?.Value 
            });
        }
    }

    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}