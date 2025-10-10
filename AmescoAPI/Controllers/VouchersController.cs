using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VouchersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        public VouchersController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("create")]
        public IActionResult CreateVoucher([FromBody] CreateVoucherRequest request)
        {
            // Check uniqueness of VoucherId
            if (_context.Vouchers.Any(v => v.VoucherId == request.VoucherId))
                return BadRequest("VoucherId already exists.");

            var points = _context.Points.FirstOrDefault(p => p.UserId == request.UserId);
            if (points == null)
            {
                return BadRequest("User does not have a points record.");
            }
            decimal pointsDeducted = request.Value;
            if (points.PointsBalance < pointsDeducted)
            {
                return BadRequest("Insufficient points balance.");
            }
            points.PointsBalance -= pointsDeducted;
            points.UpdatedAt = DateTime.Now;

            var voucher = new Voucher
            {
                VoucherId = request.VoucherId,
                UserId = request.UserId,
                VoucherCode = $"AVQR{request.VoucherId}",
                Value = request.Value,
                PointsDeducted = pointsDeducted,
                DateCreated = DateTime.Now,
                IsUsed = false,
                DateUsed = null
            };

            _context.Vouchers.Add(voucher);
            _context.SaveChanges();

            var user = _context.Users.FirstOrDefault(u => u.MemberId == request.UserId);
            string email = user?.Email ?? "";
            string memberId = user?.MemberId ?? "";
            string seriesNumber = memberId.Contains('-') ? memberId.Split('-').Last() : "";

            string qrPayload = $"{voucher.VoucherCode}\n" +
                              $"Series: {seriesNumber}\n" +
                              $"Value: {voucher.Value}\n" +
                              $"Email: {email}\n" +
                              $"Date Created: {voucher.DateCreated:yyyy-MM-dd HH:mm:ss}\n" +
                              $"Points Balance: {points.PointsBalance}";

            string base64Qr = null;
            try
            {
                using var qrGenerator = new QRCoder.QRCodeGenerator();
                using var qrData = qrGenerator.CreateQrCode(qrPayload, QRCoder.QRCodeGenerator.ECCLevel.M);
                using var qrCode = new QRCoder.PngByteQRCode(qrData);
                var qrBytes = qrCode.GetGraphic(20);
                base64Qr = Convert.ToBase64String(qrBytes);
            }
            catch { base64Qr = null; }

            return Ok(new
            {
                message = "Voucher created successfully.",
                voucher,
                newPointsBalance = points.PointsBalance,
                qrImage = base64Qr
            });
        }

        [HttpGet("count-used")]
        public IActionResult GetUsedVoucherCount()
        {
            int count = _context.Vouchers.Count(v => v.IsUsed);
            return Ok(new { count });
        }

        [HttpGet("count-details")]
        public IActionResult GetVoucherCountDetails(DateTime? start, DateTime? end)
        {
            var vouchers = _context.Vouchers.AsQueryable();

            if (start.HasValue)
                vouchers = vouchers.Where(v => v.DateCreated >= start.Value);

            if (end.HasValue)
                vouchers = vouchers.Where(v => v.DateCreated < end.Value);

            int count = vouchers.Count();
            int used = vouchers.Count(v => v.IsUsed);
            int unused = count - used;

            return Ok(new
            {
                count,
                used,
                unused
            });
        }

        [HttpGet("points-redeemers-count")]
        public IActionResult GetPointsRedeemersCount(DateTime? start, DateTime? end)
        {
            var vouchers = _context.Vouchers.AsQueryable();

            if (start.HasValue)
                vouchers = vouchers.Where(v => v.DateCreated >= start.Value);

            if (end.HasValue)
                vouchers = vouchers.Where(v => v.DateCreated < end.Value);

            var pointsRedeemers = vouchers
                .Where(v => v.IsUsed)
                .Select(v => v.UserId)
                .Distinct()
                .Count();

            int memberCount = _context.Users.Count();

            return Ok(new { pointsRedeemers, memberCount });
        }

        [HttpGet("latest-transactions")]
        public IActionResult GetLatestTransactions(DateTime? startDate, DateTime? endDate)
        {
            var vouchers = _context.Vouchers
                .Where(v => v.IsUsed);

            if (startDate.HasValue)
                vouchers = vouchers.Where(v => v.DateUsed >= startDate.Value);

            if (endDate.HasValue)
                vouchers = vouchers.Where(v => v.DateUsed < endDate.Value.Date.AddDays(1)); // Inclusive

            var latest = vouchers
                .OrderByDescending(v => v.DateUsed)
                .Take(5)
                .Select(v => new
                {
                    points = v.PointsDeducted,
                    member = _context.Users
                        .Where(u => u.MemberId == v.UserId)
                        .Select(u => $"{u.FirstName} {u.LastName}")
                        .FirstOrDefault() ?? "",
                    voucherCode = v.VoucherCode,
                    dateUsed = v.DateUsed
                })
                .ToList();

            return Ok(latest);
        }

        [HttpGet("redeemed-points")]
        public IActionResult GetRedeemedPoints(DateTime? start, DateTime? end)
        {
            var vouchers = _context.Vouchers.AsQueryable();

            if (start.HasValue)
                vouchers = vouchers.Where(v => v.DateCreated >= start.Value);

            if (end.HasValue)
                vouchers = vouchers.Where(v => v.DateCreated < end.Value.Date.AddDays(1)); // Inclusive of end date

            decimal redeemedPoints = vouchers
                .Where(v => v.IsUsed)
                .Sum(v => v.Value);

            return Ok(new { redeemedPoints });
        }

        [HttpGet("highest-redeemed-date")]
        public IActionResult GetHighestRedeemedVoucherDate(DateTime? startDate, DateTime? endDate)
        {
            var vouchers = _context.Vouchers.Where(v => v.IsUsed);

            if (startDate.HasValue)
                vouchers = vouchers.Where(v => v.DateUsed >= startDate.Value);

            if (endDate.HasValue)
                vouchers = vouchers.Where(v => v.DateUsed < endDate.Value.Date.AddDays(1)); // Inclusive

            var voucher = vouchers
                .OrderByDescending(v => v.PointsDeducted)
                .FirstOrDefault();

            if (voucher == null || voucher.DateUsed == null)
                return NotFound(new { message = "No redeemed vouchers found." });

            var date = voucher.DateUsed.Value.Date.ToString("yyyy-MM-dd");
            return Ok(new { date });
        }

        [HttpGet("top-redeemer")]
        public IActionResult GetTopRedeemer(DateTime? startDate, DateTime? endDate)
        {
            var vouchers = _context.Vouchers.AsQueryable();

            // Filter by date range and used vouchers
            if (startDate.HasValue)
                vouchers = vouchers.Where(v => v.DateUsed >= startDate.Value);
            if (endDate.HasValue)
                vouchers = vouchers.Where(v => v.DateUsed < endDate.Value.Date.AddDays(1));
            vouchers = vouchers.Where(v => v.IsUsed);

            // Group by UserId and sum PointsDeducted
            var topRedeemer = vouchers
                .GroupBy(v => v.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    PointsRedeemed = g.Sum(x => x.PointsDeducted)
                })
                .OrderByDescending(x => x.PointsRedeemed)
                .FirstOrDefault();

            if (topRedeemer == null)
                return Ok(new { name = "", pointsRedeemed = 0, profileImage = "No Image Found" });

            // Get user info
            var user = _context.Users.FirstOrDefault(u => u.MemberId == topRedeemer.UserId);
            if (user == null)
                return Ok(new { name = "", pointsRedeemed = topRedeemer.PointsRedeemed, profileImage = "No Image Found" });

            // Get profile image from AmescoImages db
            string profileImage = "No Image Found";
            try
            {
                var imagesConnectionString = _configuration.GetConnectionString("AmescoImagesConnection");
                var optionsBuilder = new DbContextOptionsBuilder<ImagesDbContext>();
                optionsBuilder.UseSqlServer(imagesConnectionString);

                using (var imagesContext = new ImagesDbContext(optionsBuilder.Options))
                {
                    var userImage = imagesContext.UserImages
                        .FirstOrDefault(ui => ui.MemberId == user.MemberId);
                    if (userImage != null && userImage.ProfileImage != null && userImage.ProfileImage.Length > 0)
                        profileImage = Convert.ToBase64String(userImage.ProfileImage);
                    else
                        profileImage = "No Image Found";
                }
            }
            catch
            {
                profileImage = "No Image Found";
            }

            return Ok(new
            {
                name = $"{user.FirstName} {user.LastName}",
                pointsRedeemed = topRedeemer.PointsRedeemed,
                profileImage
            });
        }

        [HttpDelete("delete")]
        public IActionResult DeleteVoucher([FromQuery] string voucherCode)
        {
            var voucher = _context.Vouchers.FirstOrDefault(v => v.VoucherCode == voucherCode);
            if (voucher == null)
            {
                return NotFound(new { message = "Voucher not found." });
            }

            // Credit back points to the user
            var points = _context.Points.FirstOrDefault(p => p.UserId == voucher.UserId);
            if (points != null)
            {
                points.PointsBalance += voucher.Value;
                points.UpdatedAt = DateTime.Now;
            }

            _context.Vouchers.Remove(voucher);
            _context.SaveChanges();
            return Ok(new { message = "Voucher deleted and points credited back.", newPointsBalance = points?.PointsBalance });
        }

        [HttpDelete("delete-all")]
        public IActionResult DeleteAllVouchers()
        {
            var allVouchers = _context.Vouchers.ToList();
            _context.Vouchers.RemoveRange(allVouchers);
            _context.SaveChanges();
            return Ok(new { message = "All vouchers deleted." });
        }
    }
}
