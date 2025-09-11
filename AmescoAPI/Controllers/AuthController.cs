using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;
using System.Security.Cryptography;
using System.Text;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {

            if (!isValidEmail(request.Email))
                return BadRequest("Invalid email format.");

            if (request.Password != request.ConfirmPassword)
                return BadRequest("Passwords do not match.");

            if (_context.Users.Any(u => u.Email == request.Email))
                return BadRequest("Email already registered.");

            var user = new Users
            {
                Email = request.Email,
                PasswordHash = HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Mobile = request.Mobile,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok(new { message = "Registration successful!" });
        }

        private bool isValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var pattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(email, pattern)) return false;
            var domain = email.Split('@').Length > 1 ? email.Split('@')[1] : "";
            if (domain.EndsWith(".")) return false;
            if (domain.Split('.').Length < 2) return false;
            var tld = domain.Substring(domain.LastIndexOf('.') + 1);
            if (tld.Length < 2 || !System.Text.RegularExpressions.Regex.IsMatch(tld, "^[A-Za-z]+$")) return false;
            return true;
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}
