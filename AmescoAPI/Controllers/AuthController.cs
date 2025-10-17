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

        private readonly TokenConcurrencyService _tokenConcurrency;

        public AuthController(AppDbContext context, IEmailService emailService, TokenConcurrencyService tokenConcurrency)
        {
            _context = context;
            _emailService = emailService;
            _tokenConcurrency = tokenConcurrency;
        }

        private string GenerateMemberId()
        {
            var random = new Random();
            string randomDigits = string.Concat(Enumerable.Range(0, 9)
                .Select(_ => random.Next(0, 10).ToString()));

            int lastSuffix = 0;

            if (_context.Users.Any())
            {
                // Load MemberIds into memory, then extract numeric suffixes safely
                var suffixes = _context.Users
                    .AsEnumerable() // ← this makes EF stop translating to SQL
                    .Select(u =>
                    {
                        var parts = u.MemberId.Split('-');
                        return parts.Length > 1 && int.TryParse(parts[1], out int suffix)
                            ? suffix
                            : 0;
                    })
                    .ToList();

                lastSuffix = suffixes.Max();
            }

            int nextSuffix = lastSuffix + 1;
            return $"{randomDigits}-{nextSuffix}";
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
                MemberId = request.MemberId
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            var points = new Points
            {
                UserId = user.MemberId,
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
                    UserId = user.MemberId,
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

            // generate a compact server session id
            var sessionId = TokenUtils.GenerateTokenUrlSafe(24);

            var token = TokenUtils.GenerateJwtToken(
                user.Id.ToString(),
                user.Email,
                user.FirstName,
                user.LastName,
                user.Mobile,
                user.MemberId,
                this.HttpContext.RequestServices.GetService<IConfiguration>(),
                sessionId // embed session id in token
            );

            // store token and session id (overwrites previous session -> forces previous session to be logged out)
            user.CurrentJwtToken = token;
            user.CurrentSessionId = sessionId;
            _context.SaveChanges();

            Console.WriteLine($"JWT issued for user {user.Email}: {token}");
            return Ok(new { message = "Login successful!", token, sessionId });
        }

        [Authorize]
        [HttpGet("session-status")]
        public IActionResult SessionStatus()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            var tokenSessionId = User.FindFirst("sid")?.Value;
            if (string.IsNullOrEmpty(tokenSessionId))
                return BadRequest(new { isValid = false, message = "No session id in token." });

            var isValid = _tokenConcurrency.IsSessionValidForUser(userIdClaim, tokenSessionId);
            return Ok(new { isValid });
        }

        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            if (!int.TryParse(userIdClaim, out int userId))
                return BadRequest("Invalid user ID");

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return NotFound("User not found");

            user.CurrentJwtToken = null; // Invalidate token
            user.CurrentSessionId = null; // Clear session id
            _context.SaveChanges();

            return Ok(new { message = "Logged out successfully." });
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
            <body style='font-family:Segoe UI,Arial,sans-serif;background:#f9f9f9;margin:0;padding:0;'>
                <table style='width:100%;max-width:480px;margin:auto;background:#fff;border-radius:8px;box-shadow:0 2px 8px #eee;'>
                    <tr>
                        <td style='padding:32px 32px 16px 32px;'>
                            <h2 style='color:#2a4365;margin-bottom:8px;'>Amesco Password Reset</h2>
                            <p style='font-size:16px;color:#333;margin-bottom:24px;'>
                                Hello {user.FirstName},<br>
                                You requested a password reset for your Amesco account.<br>
                                Please use the temporary password below to log in and change your password as soon as possible.
                            </p>
                            <div style='background:#e2e8f0;padding:18px 0;border-radius:6px;text-align:center;margin-bottom:24px;'>
                                <span style='font-size:22px;font-weight:600;color:#2b6cb0;letter-spacing:2px;'>{tempPassword}</span>
                            </div>
                            <p style='font-size:14px;color:#555;margin-bottom:0;'>
                                If you did not request this, please ignore this email or contact support.
                            </p>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:0 32px 24px 32px;font-size:13px;color:#888;text-align:center;'>
                            &copy; {DateTime.Now.Year} Amesco. All rights reserved.
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            ";
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

            var tokenFromRequest = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (!_tokenConcurrency.IsTokenValidForUser(userId.ToString(), tokenFromRequest))
                return Unauthorized("Session expired or logged in elsewhere.");

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
