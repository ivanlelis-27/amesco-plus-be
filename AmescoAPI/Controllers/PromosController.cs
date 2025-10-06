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
    }
}