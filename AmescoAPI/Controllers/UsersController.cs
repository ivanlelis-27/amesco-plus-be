using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using AmescoAPI.Models;
using AmescoAPI.Models.DTOs;
using Microsoft.Data.SqlClient;
using Dapper;
using AmescoAPI.Data;
using QRCoder;
using System.Linq;
using System.IO;
using Microsoft.EntityFrameworkCore;
using AmescoAPI.Services;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly string _imagesConnectionString;

        private readonly TokenConcurrencyService _tokenConcurrency;

        public UsersController(AppDbContext context, IConfiguration config, TokenConcurrencyService tokenConcurrency)
        {
            _context = context;
            _config = config;
            _imagesConnectionString = _config.GetConnectionString("AmescoImagesConnection");
            _tokenConcurrency = tokenConcurrency;
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult Me()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var tokenFromRequest = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (!_tokenConcurrency.IsTokenValidForUser(userId.ToString(), tokenFromRequest))
                return Unauthorized("Session expired or logged in elsewhere.");

            // 🔹 FIX: changed from u.Id → u.UserId
            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null) return NotFound();

            // 🔹 FIX: fetch membership using UserId
            var membership = _context.Memberships.FirstOrDefault(m => m.UserId == user.UserId);
            if (membership == null) return NotFound("Membership not found.");

            var points = _context.Points.FirstOrDefault(p => p.UserId == membership.MemberId);

            dynamic? userImage = null;
            byte[]? profileImageBytes = null;
            string? profileImageType = null;

            using (var connection = new SqlConnection(_imagesConnectionString))
            {
                userImage = connection.QueryFirstOrDefault<dynamic>(
                    @"SELECT TOP 1 ProfileImage, ImageType
              FROM UserImages 
              WHERE MemberId = @MemberId 
              ORDER BY UploadedAt DESC",
                    new { MemberId = membership.MemberId }
                );

                if (userImage != null)
                {
                    profileImageBytes = (byte[])userImage.ProfileImage;
                    profileImageType = (string)userImage.ImageType;
                }
            }

            string? profileImageBase64 = profileImageBytes != null
                ? Convert.ToBase64String(profileImageBytes)
                : null;

            return Ok(new
            {
                name = $"{user.FirstName} {user.LastName}",
                email = user.Email,
                mobile = user.Mobile,
                memberId = membership.MemberId,
                points = points?.PointsBalance ?? 0,
                profileImage = profileImageBase64,
                profileImageType = profileImageBytes != null ? "png" : null
            });
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateMeDto dto)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            if (!int.TryParse(userIdClaim, out int userId)) return BadRequest("Invalid user id in token.");

            var tokenFromRequest = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (!_tokenConcurrency.IsTokenValidForUser(userId.ToString(), tokenFromRequest))
                return Unauthorized("Session expired or logged in elsewhere.");

            // 🔹 FIX: changed from Id → UserId
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound("User not found.");

            if (!string.IsNullOrWhiteSpace(dto.FirstName)) user.FirstName = dto.FirstName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.LastName)) user.LastName = dto.LastName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Mobile)) user.Mobile = dto.Mobile.Trim();

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = user.UserId, // 🔹 FIX
                firstName = user.FirstName,
                lastName = user.LastName,
                mobile = user.Mobile
            });
        }

        public class UpdateMeDto
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Mobile { get; set; }
        }

        [HttpGet("user/{memberId}")]
        public IActionResult GetVouchersForUser(string memberId)
        {
            var vouchers = _context.Vouchers.Where(v => v.UserId == memberId).ToList();
            return Ok(vouchers);
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            Dictionary<string, byte[]> images;
            using (var connection = new SqlConnection(_imagesConnectionString))
            {
                connection.Open();
                images = connection.Query<(string MemberId, byte[] ProfileImage)>(
                    "SELECT MemberId, ProfileImage FROM UserImages"
                ).ToDictionary(x => x.MemberId, x => x.ProfileImage);
            }

            // 🔹 FIX: join with Memberships to get MemberId
            var users = _context.Users
                .Include(u => u.Membership)
                .Select(u => new
                {
                    u.UserId,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.Mobile,
                    u.CreatedAt,
                    memberId = u.Membership.MemberId,
                    points = _context.Points
                        .Where(p => p.UserId == u.Membership.MemberId)
                        .Select(p => p.PointsBalance)
                        .FirstOrDefault(),
                    profileImage = images.ContainsKey(u.Membership.MemberId)
                        ? Convert.ToBase64String(images[u.Membership.MemberId])
                        : null
                })
                .ToList();

            return Ok(users);
        }

        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null) return NotFound();
            return Ok(user);
        }

        [HttpPost]
        public IActionResult Create(Users user)
        {
            if (!IsValidEmail(user.Email))
                return BadRequest("Invalid email format.");

            user.CreatedAt = DateTime.Now;
            _context.Users.Add(user);
            _context.SaveChanges();
            return CreatedAtAction(nameof(Get), new { id = user.UserId }, user);
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var pattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(email, pattern)) return false;
            var domain = email.Split('@').Length > 1 ? email.Split('@')[1] : "";
            if (domain.Split('.').Length < 2) return false;
            var tld = domain.Substring(domain.LastIndexOf('.') + 1);
            if (tld.Length < 2) return false;
            return true;
        }

        [HttpPut("{id:int}")]
        public IActionResult Update(int id, Users updated)
        {
            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            user.FirstName = updated.FirstName;
            user.LastName = updated.LastName;
            user.Email = updated.Email;
            user.Mobile = updated.Mobile;

            _context.SaveChanges();
            return Ok(user);
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            _context.SaveChanges();
            return NoContent();
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized("Invalid token.");

            if (!int.TryParse(userIdClaim, out int userId))
                return BadRequest("Invalid user ID in token.");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found.");

            // Hash the new password
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(dto.NewPassword));
            var hashedPassword = Convert.ToBase64String(hashedBytes);

            user.PasswordHash = hashedPassword;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password updated successfully." });
        }

        public class ChangePasswordDto
        {
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpGet("qr")]
        [Authorize]
        public IActionResult GetUserQr()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            if (!int.TryParse(userIdClaim, out int userId))
                return BadRequest("Invalid user ID");

            // 🔹 FIX: changed Id → UserId
            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
                return NotFound("User not found");

            var membership = _context.Memberships.FirstOrDefault(m => m.UserId == user.UserId);
            if (membership == null)
                return NotFound("Membership not found.");

            string series = membership.MemberId.Contains('-') ? membership.MemberId.Split('-').Last() : "";
            string fullName = $"{user.FirstName} {user.LastName}";
            var points = _context.Points.FirstOrDefault(p => p.UserId == membership.MemberId);
            decimal pointsBalance = points?.PointsBalance ?? 0;

            string qrPayload = $"Series: {series}\n" +
                               $"Name: {fullName}\n" +
                               $"Email: {user.Email}\n" +
                               $"PointsBalance: {pointsBalance}";

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrData);
            var qrBytes = qrCode.GetGraphic(20);

            var base64Qr = Convert.ToBase64String(qrBytes);
            return Ok(new { qrImage = base64Qr });
        }


        [Authorize]
        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadProfileImage([FromForm] IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest("No file uploaded.");

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized("Invalid token, no userId found.");

            if (!int.TryParse(userIdClaim, out var userId))
                return BadRequest("Invalid userId in token.");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found.");

            var membership = await _context.Memberships.FirstOrDefaultAsync(m => m.UserId == user.UserId);
            if (membership == null)
                return NotFound("Membership not found for this user.");

            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);
            var imageBytes = ms.ToArray();

            string imageType = image.ContentType.Contains("png") ? "png" : "jpeg";

            using var connection = new SqlConnection(_imagesConnectionString);
            connection.Open();

            // ✅ Use membership.MemberId safely now
            var exists = await connection.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(1) FROM UserImages WHERE MemberId = @MemberId",
                new { MemberId = membership.MemberId }
            );

            if (exists > 0)
            {
                // Update existing row
                string updateQuery = @"
                    UPDATE UserImages
                    SET ProfileImage = @ProfileImage,
                        ImageType = @ImageType,
                        UploadedAt = GETDATE()
                    WHERE MemberId = @MemberId";

                await connection.ExecuteAsync(updateQuery, new
                {
                    ProfileImage = imageBytes,
                    ImageType = imageType,
                    MemberId = membership.MemberId
                });
            }
            else
            {
                // Insert new row
                string insertQuery = @"
                    INSERT INTO UserImages (MemberId, ProfileImage, ImageType, UploadedAt)
                    VALUES (@MemberId, @ProfileImage, @ImageType, GETDATE())";

                await connection.ExecuteAsync(insertQuery, new
                {
                    MemberId = membership.MemberId,
                    ProfileImage = imageBytes,
                    ImageType = imageType
                });
            }

            return Ok(new { message = "Profile image uploaded successfully!" });
        }


        [HttpDelete("clear-user-images")]
        [Authorize]
        public IActionResult ClearUserImages()
        {
            try
            {
                using (var connection = new SqlConnection(_imagesConnectionString))
                {
                    connection.Open();
                    var rowsAffected = connection.Execute("DELETE FROM dbo.UserImages");
                    return Ok(new { message = $"Deleted {rowsAffected} rows from UserImages." });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error clearing UserImages: " + ex.Message);
                return StatusCode(500, new { error = "Failed to clear UserImages." });
            }
        }

        [HttpGet("count")]
        public IActionResult GetUserCount()
        {
            int count = _context.Users.Count();
            return Ok(new { count });
        }

        [HttpGet("new-members")]
        public IActionResult GetNewMembersCountByDateRange(DateTime? startDate, DateTime? endDate)
        {
            // If no dates are provided, default to last 30 days
            var from = startDate ?? DateTime.Now.AddDays(-30);
            var to = endDate ?? DateTime.Now;

            int count = _context.Users.Count(u => u.CreatedAt >= from && u.CreatedAt <= to);
            return Ok(new { count });
        }

    }


}
