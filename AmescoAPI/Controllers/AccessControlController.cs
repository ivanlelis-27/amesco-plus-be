using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;
using AmescoAPI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccessControlController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        public AccessControlController(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var users = _context.AccessControls.ToList();
            return Ok(users);
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] AccessControlLoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Email and password are required.");

            var user = _context.AccessControls.FirstOrDefault(u => u.Email == request.Email);
            if (user == null) return NotFound("User not found.");

            // Hash incoming password same as Create/Reset uses (SHA256) and compare
            var incomingHash = HashPassword(request.Password);
            if (!string.Equals(incomingHash, user.PasswordHash))
                return Unauthorized("Invalid credentials.");

            // update last login timestamp
            user.LastLogin = DateTime.Now;
            _context.SaveChanges();

            // generate session id and JWT (access-control users)
            var sessionId = TokenUtils.GenerateTokenUrlSafe(24);
            var names = (user.FullName ?? "").Split(' ', 2, System.StringSplitOptions.RemoveEmptyEntries);
            var firstName = names.Length > 0 ? names[0] : "";
            var lastName = names.Length > 1 ? names[1] : "";
            var config = HttpContext.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;

            var token = TokenUtils.GenerateJwtToken(
                user.UserID.ToString(),
                user.Email,
                firstName,
                lastName,
                "",   // mobile (not used for access control)
                "",   // memberId (not used for access control)
                config,
                sessionId
            );

            // ensure Authorization header contains the token (overwrite if present)
            Response.Headers["Authorization"] = "Bearer " + token;

            // expose header so browsers can read it
            if (Response.Headers.TryGetValue("Access-Control-Expose-Headers", out var existing))
            {
                var expose = existing.ToString();
                if (!expose.Contains("Authorization"))
                    Response.Headers["Access-Control-Expose-Headers"] = expose + ", Authorization";
            }
            else
            {
                Response.Headers.Add("Access-Control-Expose-Headers", "Authorization");
            }

            return Ok(new
            {
                id = user.UserID,
                fullName = user.FullName,
                email = user.Email,
                role = user.Role,
                branchID = user.BranchID,
                lastLogin = user.LastLogin,
                token,
                sessionId
            });
        }

        public class AccessControlLoginRequest
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }

        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            // try standard name identifier claim then fallback to "sub"
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            if (!int.TryParse(userIdClaim, out int userId))
                return BadRequest("Invalid user ID");

            var user = _context.AccessControls.FirstOrDefault(u => u.UserID == userId);
            if (user == null) return NotFound("User not found.");

            // If you later store tokens for AccessControl users, clear them here.
            Console.WriteLine($"AccessControl logout: user {user.Email} (ID {user.UserID})");

            return Ok(new { message = "Logout successful" });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Email))
                return BadRequest(new { message = "Invalid email." });

            var user = _context.AccessControls.FirstOrDefault(u => u.Email == request.Email);
            if (user == null) return BadRequest(new { message = "Invalid email." });

            // reuse existing ResetPassword logic
            return await ResetPassword(request);
        }


        [HttpPost]
        public IActionResult Create([FromBody] CreateAccessControlRequest request)
        {
            var hashedPassword = HashPassword(request.PasswordHash); 

            var user = new AccessControl
            {
                FullName = $"{request.FirstName} {request.LastName}",
                Email = request.Email,
                PasswordHash = hashedPassword,
                Role = request.Role,
                BranchID = request.BranchID,
                CreatedAt = DateTime.Now,
                IsActive = true,
                LastLogin = null
            };
            _context.AccessControls.Add(user);
            _context.SaveChanges();
            return Ok(user);
        }
        private string HashPassword(string password)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] AccessControl updated)
        {
            var user = _context.AccessControls.Find(id);
            if (user == null) return NotFound();

            user.FullName = updated.FullName;
            user.Email = updated.Email;
            user.PasswordHash = updated.PasswordHash;
            user.Role = updated.Role;
            user.BranchID = updated.BranchID;
            user.LastLogin = updated.LastLogin;
            user.IsActive = updated.IsActive;

            _context.SaveChanges();
            return Ok(user);
        }

        [HttpPatch("{id}")]
        public IActionResult EditBasicInfo(int id, [FromBody] EditAccessControlRequest request)
        {
            var user = _context.AccessControls.Find(id);
            if (user == null) return NotFound();

            user.FullName = $"{request.FirstName} {request.LastName}";
            user.Email = request.Email;
            user.BranchID = request.BranchID;
            user.Role = request.Role;

            _context.SaveChanges();
            return Ok(user);
        }

        public class EditAccessControlRequest
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }
            public int BranchID { get; set; }
            public string Role { get; set; }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var user = _context.AccessControls.FirstOrDefault(u => u.Email == request.Email);
            if (user == null) return NotFound("User not found.");

            var tempPassword = GenerateTemporaryPassword(9);
            var hashedPassword = HashPassword(tempPassword);

            user.PasswordHash = hashedPassword;
            _context.SaveChanges();

            var subject = "Your Temporary Password";

            var htmlBody = $@"
            <table style='width:100%;max-width:480px;margin:auto;font-family:Segoe UI,Arial,sans-serif;background:#f9f9f9;border-radius:8px;box-shadow:0 2px 8px #eee;'>
                <tr>
                    <td style='padding:32px 32px 16px 32px;'>
                        <h2 style='color:#2a4365;margin-bottom:8px;'>Amesco Password Reset</h2>
                        <p style='font-size:16px;color:#333;margin-bottom:24px;'>
                            Hello,<br>
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
            ";

            await _emailService.SendEmailAsync(user.Email, subject, htmlBody);

            return Ok(new { message = "Temporary password sent to your email." });
        }

        public class ResetPasswordRequest
        {
            public string Email { get; set; }
        }

        private string GenerateTemporaryPassword(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var user = _context.AccessControls.Find(id);
            if (user == null) return NotFound();
            _context.AccessControls.Remove(user);
            _context.SaveChanges();
            return NoContent();
        }


        // test endpoint to change password by user ID only
        [HttpPost("change-password/{id}")]
        public IActionResult ChangePasswordById(int id, [FromBody] ChangePasswordRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { message = "NewPassword is required." });

            var user = _context.AccessControls.Find(id);
            if (user == null) return NotFound(new { message = "User not found." });

            user.PasswordHash = HashPassword(request.NewPassword);
            _context.SaveChanges();

            return Ok(new { message = "Password updated (temporary test endpoint)." });
        }

        public class ChangePasswordRequest
        {
            public string NewPassword { get; set; }
        }
    }
}