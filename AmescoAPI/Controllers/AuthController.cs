using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;
using AmescoAPI.Models.Auth;
using AmescoAPI.Services;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

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
            int randomDigits = random.Next(1000, 10000); // 4 digits
            return $"588303500-{randomDigits}";
        }

        public AuthController(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // ✅ Register
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
                CreatedAt = DateTime.Now,
                MemberId = GenerateMemberId()
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            // Create Points row for new user
            var points = new Points
            {
                UserId = user.Id,
                PointsBalance = 0,
                UpdatedAt = DateTime.Now
            };
            _context.Points.Add(points);
            _context.SaveChanges();
            Console.WriteLine($"User created: {user.Id}, {user.Email}");
            Console.WriteLine($"Points row created for UserId: {points.UserId}, PointsBalance: {points.PointsBalance}");

            return Ok(new { message = "Registration successful!" });
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

        // ✅ Forgot Password → sends TEMPORARY password
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
            if (user == null) return NotFound("No user with that email.");

            // generate random temp password
            var tempPassword = GenerateTempPassword();
            user.PasswordHash = HashPassword(tempPassword);
            _context.SaveChanges();

            // send email
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

        // ✅ Reset Password → user logs in with temp pw, then sets NEW password
        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
            if (user == null) return NotFound("User not found.");

            user.PasswordHash = HashPassword(request.NewPassword);
            _context.SaveChanges();

            return Ok(new { message = "Password has been reset successfully!" });
        }

        // --- Helpers ---
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
