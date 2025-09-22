using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThumbnailService.Models;
using ThumbnailService.Services;

namespace ThumbnailService.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IEncryptionService _encryption;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            AppDbContext db, 
            IEncryptionService encryption, 
            IJwtService jwtService,
            ILogger<AccountController> logger)
        {
            _db = db;
            _encryption = encryption;
            _jwtService = jwtService;
            _logger = logger;
        }

        // ---------------------------
        // Register
        // ---------------------------
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Email and password required");
                return View();
            }

            var exists = await _db.Users.AnyAsync(u => u.Email == email);
            if (exists)
            {
                ModelState.AddModelError(string.Empty, "Email already registered");
                return View();
            }

            // Encrypt password
            string encryptedPassword;
            try
            {
                encryptedPassword = await _encryption.EncryptAsync(password);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password encryption for {Email}", email);
                ModelState.AddModelError(string.Empty, "Internal error, please try again later");
                return View();
            }

            var user = new User
            {
                Email = email,
                EncryptedPassword = encryptedPassword
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("User {Email} registered successfully", email);

            // Redirect to Login page instead of returning JWT immediately
            TempData["Success"] = "Registration successful! Please login.";
            return RedirectToAction("Login");
        }

        // ---------------------------
        // Login
        // ---------------------------
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View();
            }

            // Decrypt password with KMS
            var decryptedPassword = await _encryption.DecryptAsync(user.EncryptedPassword);
            if (decryptedPassword != password)
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View();
            }

            // Issue JWT
            var jwt = await _jwtService.GenerateTokenAsync(user.Id, user.Email);

            // 👉 Option 1: Return JSON (API style)
            // return Json(new { token = jwt });

            // 👉 Option 2: Set JWT as HttpOnly Cookie (MVC style)
            Response.Cookies.Append("AuthToken", jwt, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = true, // chỉ gửi qua HTTPS
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddHours(1)
            });

            _logger.LogInformation("User {Email} logged in successfully", email);

            return RedirectToAction("Index", "Home");
        }

        // ---------------------------
        // Logout
        // ---------------------------
        [HttpPost]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("AuthToken");
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied() => View();
    }
}
