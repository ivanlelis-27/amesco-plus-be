using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;
using AmescoAPI.Models.Auth;
using AmescoAPI.Services;
using QRCoder;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Authorization;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        private string GenerateMemberId()
        {
            var random = new Random();
            string randomDigits = string.Concat(Enumerable.Range(0, 9).Select(_ => random.Next(0, 10).ToString()));
            int seriesCounter = _context.Users.Count() + 1;
            return $"{randomDigits}-{seriesCounter}";
        }

        public AuthController(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
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

            if (string.IsNullOrWhiteSpace(request.MemberId))
                return BadRequest("MemberId is required.");

            var user = new Users
            {
                Email = request.Email,
                PasswordHash = HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Mobile = request.Mobile,
                CreatedAt = DateTime.Now,
                MemberId = request.MemberId // <-- Use the memberId from the request!
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            var points = new Points
            {
                UserId = user.Id,
                PointsBalance = 0,
                UpdatedAt = DateTime.Now
            };
            _context.Points.Add(points);
            _context.SaveChanges();

            return Ok(new { message = "Registration successful!" });
        }

        [HttpGet("generate-memberid")]
        public IActionResult GenerateMemberIdApi()
        {
            var memberId = GenerateMemberId();
            return Ok(new { memberId });
        }

        [HttpPost("bulk-register")]
        public IActionResult BulkRegister([FromBody] List<RegisterRequest> requests)
        {
            var createdUsers = new List<object>();

            foreach (var request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password) ||
                    string.IsNullOrWhiteSpace(request.FirstName) ||
                    string.IsNullOrWhiteSpace(request.LastName) ||
                    string.IsNullOrWhiteSpace(request.MemberId))
                {
                    continue; // skip invalid entries
                }

                if (_context.Users.Any(u => u.Email == request.Email))
                {
                    continue; // skip duplicates
                }

                var user = new Users
                {
                    Email = request.Email,
                    PasswordHash = HashPassword(request.Password),
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Mobile = request.Mobile,
                    CreatedAt = DateTime.Now,
                    MemberId = request.MemberId
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                var points = new Points
                {
                    UserId = user.Id,
                    PointsBalance = 0,
                    UpdatedAt = DateTime.Now
                };
                _context.Points.Add(points);
                _context.SaveChanges();

                createdUsers.Add(new { user.Id, user.Email, user.MemberId });
            }

            return Ok(new { count = createdUsers.Count, users = createdUsers });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
            if (user == null) return NotFound("User not found.");

            if (user.PasswordHash != HashPassword(request.Password))
                return BadRequest("Invalid password.");

            var token = TokenUtils.GenerateJwtToken(
            user.Id.ToString(),
            user.Email,
            user.FirstName,
            user.LastName,
            user.Mobile,
            user.MemberId,
            this.HttpContext.RequestServices.GetService<IConfiguration>());
            Console.WriteLine($"JWT issued for user {user.Email}: {token}");
            return Ok(new { message = "Login successful!", token });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
            if (user == null) return NotFound("No user with that email.");


            var tempPassword = GenerateTempPassword();
            user.PasswordHash = HashPassword(tempPassword);
            _context.SaveChanges();

            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>Hello {user.FirstName},</h2>
                    <p>Your temporary password is: <b>{tempPassword}</b></p>
                    <p>Please log in with this password and reset it immediately.</p>
                    <br/>
                    <p style='color:gray;'>– Amesco Support</p>
                </body>
                </html>";
            Console.WriteLine("Sending Email Body:"); // for debugging
            Console.WriteLine(body);
            await _emailService.SendEmailAsync(user.Email, "Your Temporary Password", body);

            return Ok(new { message = "Temporary password sent to your email." });
        }

        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
            if (user == null) return NotFound("User not found.");

            user.PasswordHash = HashPassword(request.NewPassword);
            _context.SaveChanges();

            return Ok(new { message = "Password has been reset successfully!" });
        }

        [Authorize]
        [HttpDelete("unsubscribe")]
        public IActionResult Unsubscribe()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            if (!int.TryParse(userIdClaim, out int userId))
                return BadRequest("Invalid user ID");

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return NotFound("User not found");

            _context.Users.Remove(user);
            _context.SaveChanges();
            Console.WriteLine($"User deleted: {user.Id}, {user.Email}");
            return Ok(new { message = "Account deleted successfully." });
        }

        private bool isValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var pattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$";
            return System.Text.RegularExpressions.Regex.IsMatch(email, pattern);
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        private string GenerateTempPassword()
        {
            return Guid.NewGuid().ToString("N")[..8]; // 8-char random temp password
        }
    }
}
