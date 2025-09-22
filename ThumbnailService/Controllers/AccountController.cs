using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThumbnailService.Models;
using ThumbnailService.Services;

namespace ThumbnailService.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IEncryptionService _encryption;
        private readonly IJwtService _jwtService;

        public AccountController(AppDbContext db, IEncryptionService encryption, IJwtService jwtService)
        {
            _db = db;
            _encryption = encryption;
            _jwtService = jwtService;
        }

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

            var user = new User
            {
                Email = email,
                EncryptedPassword = await _encryption.EncryptAsync(password);
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Issue JWT
            var jwt = await _jwtService.GenerateTokenAsync(user.Id, user.Email);
            return Json(new { token = jwt });
        }

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

            var decrypted = await _encryption.DecryptAsync(user.EncryptedPassword);

            if (decrypted != password)
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View();
            }

            // Issue JWT
            var jwt = await _jwtService.GenerateTokenAsync(user.Id, user.Email);
            return Json(new { token = jwt });
        }

        [HttpPost]
        public IActionResult Logout()
        {
            // For JWT, logout is handled client-side (delete token)
            return Ok();
        }

        public IActionResult AccessDenied() => View();
    }
}


