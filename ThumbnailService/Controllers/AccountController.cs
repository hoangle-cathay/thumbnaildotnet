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

        // ---------------------------
        // Register
        // ---------------------------
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(string email, string password)
        {
            _logger.LogInformation("Registration attempt for email: {Email}", email);
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Registration failed: missing email or password");
                ModelState.AddModelError(string.Empty, "Email and password required");
                return View();
            }

            var exists = await _db.Users.AnyAsync(u => u.Email == email);
            if (exists)
            {
                _logger.LogWarning("Registration failed: email {Email} already registered", email);
                ModelState.AddModelError(string.Empty, "Email already registered");
                return View();
            }

            // Encrypt password
            string encryptedPassword;
            try
            {
                _logger.LogInformation("Encrypting password for {Email}", email);
                encryptedPassword = await _encryption.EncryptAsync(password);
                _logger.LogInformation("Password encrypted for {Email}", email);
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

            _logger.LogInformation("User {Email} registered successfully with Id {UserId}", email, user.Id);

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
            _logger.LogInformation("Login attempt for email: {Email}", email);
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                _logger.LogWarning("Login failed: user {Email} not found", email);
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View();
            }

            // Decrypt password with KMS
            _logger.LogInformation("Decrypting password for {Email}", email);
            var decryptedPassword = await _encryption.DecryptAsync(user.EncryptedPassword);
            if (decryptedPassword != password)
            {
                _logger.LogWarning("Login failed: invalid password for {Email}", email);
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View();
            }

            // Set a dummy cookie or implement your new cookie-based auth here
            // For now, just log in and redirect
            _logger.LogInformation("User {Email} logged in successfully (Id: {UserId})", email, user.Id);
            return RedirectToAction("Index", "Home");
        }

        // ---------------------------
        // Logout
        // ---------------------------
        [HttpPost]
        public IActionResult Logout()
        {
            _logger.LogInformation("User logged out");
            Response.Cookies.Delete("AuthToken");
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied() => View();
    }
}
