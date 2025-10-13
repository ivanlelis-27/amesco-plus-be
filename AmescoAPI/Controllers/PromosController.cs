using AmescoAPI.Models;
using AmescoAPI.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PromosController : ControllerBase
    {
        private readonly AppDbContext _mainDb;
        private readonly ImagesDbContext _imagesDb;

        public PromosController(AppDbContext mainDb, ImagesDbContext imagesDb)
        {
            _mainDb = mainDb;
            _imagesDb = imagesDb;
        }

        [HttpPost]
        [Route("upload")]
        public async Task<IActionResult> UploadPromo([FromForm] CreatePromoRequest request)
        {
            if (request.Image == null || request.Image.Length == 0)
                return BadRequest("Image is required.");

            // Save promo to main DB
            var promo = new Promo
            {
                BrandItemName = request.BrandItemName,
                Price = request.Price,
                Unit = request.Unit,
                CreatedAt = DateTime.UtcNow
            };
            _mainDb.Add(promo);
            await _mainDb.SaveChangesAsync();

            // Save image to images DB
            using var ms = new MemoryStream();
            await request.Image.CopyToAsync(ms);

            var promoImage = new PromoImage
            {
                PromoId = promo.PromoId,
                ImageData = ms.ToArray(),
                CreatedAt = DateTime.UtcNow
            };
            _imagesDb.Add(promoImage);
            await _imagesDb.SaveChangesAsync();

            return Ok(new { promo.PromoId });
        }

        [HttpGet]
        [Route("list")]
        public async Task<IActionResult> GetPromos()
        {
            var promos = await _mainDb.Promos
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var promoIds = promos.Select(p => p.PromoId).ToList();
            var images = await _imagesDb.PromoImages
                .Where(img => promoIds.Contains(img.PromoId))
                .ToListAsync();

            var result = promos.Select(p => new
            {
                p.PromoId,
                p.BrandItemName,
                p.Price,
                p.Unit,
                p.CreatedAt,
                ImageBase64 = images.FirstOrDefault(img => img.PromoId == p.PromoId)?.ImageData != null
                    ? Convert.ToBase64String(images.FirstOrDefault(img => img.PromoId == p.PromoId).ImageData)
                    : null
            });

            return Ok(result);
        }

        [HttpGet("with-group")]
        public async Task<IActionResult> GetPromosWithGroup()
        {
            var promos = await _mainDb.Promos
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var promoIds = promos.Select(p => p.PromoId).ToList();

            var images = await _imagesDb.PromoImages
                .Where(img => promoIds.Contains(img.PromoId))
                .ToListAsync();

            // Get all AnnouncementProducts for these promos
            var announcementProducts = _mainDb.AnnouncementProducts
                .Where(ap => promoIds.Contains(ap.PromoId))
                .ToList();

            // Get all relevant Announcements
            var announcementIds = announcementProducts.Select(ap => ap.AnnouncementId).Distinct().ToList();
            var announcements = _mainDb.Announcements
                .Where(a => announcementIds.Contains(a.AnnouncementId))
                .ToList();

            // Get all relevant PromoGroups
            var promoGroupIds = announcements
                .Where(a => a.PromoGroupId.HasValue)
                .Select(a => a.PromoGroupId.Value)
                .Distinct()
                .ToList();
            var promoGroups = _mainDb.PromoGroups
                .Where(pg => promoGroupIds.Contains(pg.PromoGroupId))
                .ToList();

            var result = promos.Select(p =>
            {
                // Find all announcements this promo is linked to
                var aps = announcementProducts.Where(ap => ap.PromoId == p.PromoId).ToList();
                var groupNames = aps
                    .Select(ap =>
                    {
                        var announcement = announcements.FirstOrDefault(a => a.AnnouncementId == ap.AnnouncementId);
                        if (announcement != null && announcement.PromoGroupId.HasValue)
                        {
                            return promoGroups.FirstOrDefault(pg => pg.PromoGroupId == announcement.PromoGroupId.Value)?.Name;
                        }
                        return null;
                    })
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .ToList();

                return new
                {
                    p.PromoId,
                    p.BrandItemName,
                    p.Price,
                    p.Unit,
                    p.CreatedAt,
                    ImageBase64 = images.FirstOrDefault(img => img.PromoId == p.PromoId)?.ImageData != null
                        ? Convert.ToBase64String(images.FirstOrDefault(img => img.PromoId == p.PromoId).ImageData)
                        : null,
                    promoGroupNames = groupNames // <-- List of all promo group names
                };
            });

            return Ok(result);
        }

        [HttpDelete]
        [Route("delete/{promoId}")]
        public async Task<IActionResult> DeletePromo(int promoId)
        {
            // Remove promo from main DB
            var promo = await _mainDb.Promos.FindAsync(promoId);
            if (promo == null)
                return NotFound("Promo not found.");

            _mainDb.Promos.Remove(promo);
            await _mainDb.SaveChangesAsync();

            // Remove associated image(s) from images DB
            var images = _imagesDb.PromoImages.Where(img => img.PromoId == promoId);
            _imagesDb.PromoImages.RemoveRange(images);
            await _imagesDb.SaveChangesAsync();

            return Ok(new { message = "Promo deleted successfully." });
        }
    }
}