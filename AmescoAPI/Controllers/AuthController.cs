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
using System.Collections.Generic;

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

            if (_context.Memberships.Any())
            {
                var suffixes = _context.Memberships
                    .AsEnumerable()
                    .Select(m =>
                    {
                        var parts = m.MemberId.Split('-');
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

            var user = new Users
            {
                Email = request.Email,
                PasswordHash = HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Mobile = request.Mobile,
                CreatedAt = DateTime.Now,
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            var memberId = string.IsNullOrWhiteSpace(request.MemberId)
                ? GenerateMemberId()
                : request.MemberId;

            var membership = new Memberships
            {
                MemberId = memberId,
                UserId = user.UserId
            };

            _context.Memberships.Add(membership);
            _context.SaveChanges();

            var points = new Points
            {
                UserId = memberId,
                PointsBalance = 0,
                UpdatedAt = DateTime.Now
            };

            _context.Points.Add(points);
            _context.SaveChanges();

            return Ok(new
            {
                message = "Registration successful!",
                userId = user.UserId,
                memberId
            });
        }

        [HttpGet("generate-memberid")]
        public IActionResult GenerateMemberIdApi()
        {
            var memberId = GenerateMemberId();
            return Ok(new { memberId });
        }

        // endpoint for testing only
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
                    continue;
                }

                var user = new Users
                {
                    Email = request.Email,
                    PasswordHash = HashPassword(request.Password),
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Mobile = request.Mobile,
                    CreatedAt = DateTime.Now,
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                // Create MemberId for membership
                var memberId = string.IsNullOrWhiteSpace(request.MemberId)
                    ? GenerateMemberId()
                    : request.MemberId;

                // Create Membership entry
                var membership = new Memberships
                {
                    MemberId = memberId,
                    UserId = user.UserId
                };
                _context.Memberships.Add(membership);
                _context.SaveChanges();

                var points = new Points
                {
                    UserId = membership.MemberId,
                    PointsBalance = 0,
                    UpdatedAt = DateTime.Now
                };
                _context.Points.Add(points);
                _context.SaveChanges();

                createdUsers.Add(new { user.UserId, user.Email, membership.MemberId });
            }

            return Ok(new { count = createdUsers.Count, users = createdUsers });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
            if (user == null) return NotFound("User not found.");

            if (!VerifyPasswordAndRehash(user, request.Password))
                return BadRequest("Invalid password.");

            var membership = _context.Memberships.FirstOrDefault(m => m.UserId == user.UserId);
            var memberId = membership?.MemberId ?? "N/A";

            // generate a compact server session id
            var sessionId = TokenUtils.GenerateTokenUrlSafe(24);

            var token = TokenUtils.GenerateJwtToken(
                user.UserId.ToString(),
                user.Email,
                user.FirstName,
                user.LastName,
                user.Mobile,
                memberId,
                this.HttpContext.RequestServices.GetService<IConfiguration>(),
                sessionId
            );

            var session = new UserSessions
            {
                SessionId = sessionId,
                UserId = user.UserId,
                JwtToken = token
            };
            _context.UserSessions.Add(session);
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

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
                return NotFound("User not found");

            var sessions = _context.UserSessions.Where(s => s.UserId == userId).ToList();
            if (sessions.Any())
            {
                _context.UserSessions.RemoveRange(sessions);
                _context.SaveChanges();
            }
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

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
                return NotFound("User not found");

            // ✅ ADDED: get the user's membership record so we can access MemberId
            var membership = _context.Memberships.FirstOrDefault(m => m.UserId == userId);
            if (membership == null)
                return NotFound("Membership not found for user.");

            using var tx = _context.Database.BeginTransaction();
            try
            {
                // ✅ FIXED: now that we have membership, we can delete points tied to MemberId
                var points = _context.Points.Where(p => p.UserId == membership.MemberId).ToList();
                if (points.Any())
                {
                    _context.Points.RemoveRange(points);
                }

                // ✅ also remove any user sessions if you want a full cleanup
                var sessions = _context.UserSessions.Where(s => s.UserId == userId).ToList();
                if (sessions.Any())
                {
                    _context.UserSessions.RemoveRange(sessions);
                }

                // ✅ remove membership itself
                _context.Memberships.Remove(membership);

                // ✅ finally remove the user
                _context.Users.Remove(user);
                _context.SaveChanges();

                tx.Commit();
                Console.WriteLine($"User deleted: {user.UserId}, {user.Email}");
                return Ok(new { message = "Account deleted successfully." });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Console.WriteLine("Unsubscribe error: " + ex.Message);
                return StatusCode(500, new { error = "Failed to delete account." });
            }
        }

        private bool isValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var pattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$";
            return System.Text.RegularExpressions.Regex.IsMatch(email, pattern);
        }

        private string HashPassword(string password)
        {
            // PBKDF2 (HMAC-SHA256) with per-user random salt.
            const int iterations = 100_000;
            const int saltSize = 16;
            const int hashSize = 32;

            var salt = new byte[saltSize];
            RandomNumberGenerator.Fill(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(hashSize);
            // stored format: iterations.saltBase64.hashBase64
            return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        private bool VerifyPasswordAndRehash(Users user, string providedPassword)
        {
            var storedHash = user?.PasswordHash;
            if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(providedPassword)) return false;

            // Legacy SHA256 stored format: no '.' separator
            if (!storedHash.Contains('.'))
            {
                using var sha256 = SHA256.Create();
                var computed = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(providedPassword)));

                var match = CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computed),
                    Encoding.UTF8.GetBytes(storedHash));

                if (match)
                {
                    try
                    {
                        // rehash with PBKDF2 and persist
                        user.PasswordHash = HashPassword(providedPassword);
                        _context.SaveChanges();
                    }
                    catch
                    {
                        // do not block login if rehash save fails
                    }
                }

                return match;
            }

            // PBKDF2 stored format: iterations.salt.hash
            var parts = storedHash.Split('.');
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], out int iterations)) return false;

            var salt = Convert.FromBase64String(parts[1]);
            var hash = Convert.FromBase64String(parts[2]);

            using var pbkdf2 = new Rfc2898DeriveBytes(providedPassword, salt, iterations, HashAlgorithmName.SHA256);
            var computedHash = pbkdf2.GetBytes(hash.Length);

            return CryptographicOperations.FixedTimeEquals(computedHash, hash);
        }

        private string GenerateTempPassword(int length = 9)
        {
            const string lowers = "abcdefghijklmnopqrstuvwxyz";
            const string uppers = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string symbols = "!@#$%^&*()-_=+[]{}<>?";

            using var rng = RandomNumberGenerator.Create();
            var chars = new List<char>
            {
                PickRandomChar(lowers, rng),
                PickRandomChar(uppers, rng),
                PickRandomChar(digits, rng),
                PickRandomChar(symbols, rng)
            };

            var all = lowers + uppers + digits + symbols;
            while (chars.Count < length) chars.Add(PickRandomChar(all, rng));

            // Fisher–Yates shuffle (secure)
            for (int i = chars.Count - 1; i > 0; i--)
            {
                var b = new byte[4];
                rng.GetBytes(b);
                int j = (int)(BitConverter.ToUInt32(b, 0) % (uint)(i + 1));
                var tmp = chars[i]; chars[i] = chars[j]; chars[j] = tmp;
            }

            return new string(chars.ToArray());
        }

        private static char PickRandomChar(string set, RandomNumberGenerator rng)
        {
            var b = new byte[4];
            rng.GetBytes(b);
            int idx = (int)(BitConverter.ToUInt32(b, 0) % (uint)set.Length);
            return set[idx];
        }
    }
}
