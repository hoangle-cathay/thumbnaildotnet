using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThumbnailService.Models;
using ThumbnailService.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace ThumbnailService.Controllers
{
    [Authorize]
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
        [AllowAnonymous]
        public IActionResult Register() => View();

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Register(string email, string password)
        {
            _logger.LogInformation("Registration attempt for email: {Email}", email);
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Registration failed: missing email or password");
                ModelState.AddModelError(string.Empty, "Email and password required");
                return View();
            }
            
            try
            {
                var exists = await _db.Users.AnyAsync(u => u.Email == email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fail to connect to DB.");
            }

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
        [AllowAnonymous]
        public IActionResult Login() => View();

        [HttpPost]
        [AllowAnonymous]
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

            // Sign in with cookie auth
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            _logger.LogInformation("User {Email} logged in successfully (Id: {UserId})", email, user.Id);
            return RedirectToAction("Index", "Home");
        }

        // ---------------------------
        // Logout
        // ---------------------------
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("User logged out");
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied() => View();
    }
}
