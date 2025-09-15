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

        public AccountController(AppDbContext db, IEncryptionService encryption)
        {
            _db = db;
            _encryption = encryption;
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
                EncryptedPassword = _encryption.Encrypt(password)
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return RedirectToAction("Login");
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

            var decrypted = _encryption.Decrypt(user.EncryptedPassword);
            if (decrypted != password)
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials");
                return View();
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied() => View();
    }
}


