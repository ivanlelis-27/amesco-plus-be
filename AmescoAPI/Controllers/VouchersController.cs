using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;
using System;
using System.Linq;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VouchersController : ControllerBase
    {
        private readonly AppDbContext _context;
        public VouchersController(AppDbContext context)
        {
            _context = context;
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

            // Get user and series number
            var user = _context.Users.FirstOrDefault(u => u.Id == request.UserId);
            string email = user?.Email ?? "";
            string memberId = user?.MemberId ?? "";
            string seriesNumber = memberId.Contains('-') ? memberId.Split('-').Last() : "";

            // Build QR payload (vertical format)
            string qrPayload = $"{voucher.VoucherCode}\n" +
                              $"Series: {seriesNumber}\n" +
                              $"Value: {voucher.Value}\n" +
                              $"Email: {email}\n" +
                              $"Date Created: {voucher.DateCreated:yyyy-MM-dd HH:mm:ss}\n" +
                              $"Points Balance: {points.PointsBalance}";

            // Generate QR code PNG and convert to Base64
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
    }
}
