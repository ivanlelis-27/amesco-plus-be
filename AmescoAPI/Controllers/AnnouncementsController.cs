using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;
using System.Threading.Tasks;
using System.IO;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnnouncementsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ImagesDbContext _imagesDb;

        public AnnouncementsController(AppDbContext context, ImagesDbContext imagesDb)
        {
            _context = context;
            _imagesDb = imagesDb;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateAnnouncement(
            [FromForm] string title,
            [FromForm] string description,
            [FromForm] int? promoGroupId,
            [FromForm] IFormFile? image)
        {
            int? bannerImageId = null;

            if (image != null && image.Length > 0)
            {
                using var ms = new MemoryStream();
                await image.CopyToAsync(ms);

                var adBanner = new AdBanner
                {
                    FileName = image.FileName,
                    ContentType = image.ContentType,
                    ImageData = ms.ToArray(),
                    CreatedAt = DateTime.UtcNow
                };

                _imagesDb.AdBanners.Add(adBanner);
                await _imagesDb.SaveChangesAsync();
                bannerImageId = adBanner.Id;
            }

            var announcement = new Announcement
            {
                Title = title,
                Description = description,
                PromoGroupId = promoGroupId,
                BannerImageId = bannerImageId,
                DateCreated = DateTime.Now,
                DateUpdated = null
            };

            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();

            return Ok(new { announcement.AnnouncementId });
        }
    }
}