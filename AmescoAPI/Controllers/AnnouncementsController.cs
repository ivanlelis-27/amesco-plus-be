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
            [FromForm] IFormFile? image,
            [FromForm] int? sortIndex,
            [FromForm] List<int> promoIds // <-- Accept promoIds from the request
        )
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
                DateUpdated = null,
                SortIndex = sortIndex
            };

            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();

            // Insert into AnnouncementProducts table
            if (promoIds != null && promoIds.Count > 0)
            {
                foreach (var promoId in promoIds)
                {
                    var ap = new AnnouncementProduct
                    {
                        AnnouncementId = announcement.AnnouncementId,
                        PromoId = promoId
                    };
                    _context.Set<AnnouncementProduct>().Add(ap);
                }
                await _context.SaveChangesAsync();
            }

            return Ok(new { announcement.AnnouncementId });
        }

        [HttpGet]
        public IActionResult GetAllAnnouncements()
        {
            var announcements = _context.Announcements
                .OrderByDescending(a => a.DateCreated)
                .ToList();

            var bannerIds = announcements
                .Where(a => a.BannerImageId.HasValue)
                .Select(a => a.BannerImageId.Value)
                .Distinct()
                .ToList();

            var banners = _imagesDb.AdBanners
                .Where(b => bannerIds.Contains(b.Id))
                .ToList();

            var result = announcements.Select(a => new
            {
                a.AnnouncementId,
                a.Title,
                a.Description,
                a.PromoGroupId,
                a.BannerImageId,
                a.DateCreated,
                a.DateUpdated,
                a.SortIndex, // <-- Include SortIndex in response
                imageBase64 = a.BannerImageId.HasValue
                    ? banners.FirstOrDefault(b => b.Id == a.BannerImageId.Value)?.ImageData != null
                        ? Convert.ToBase64String(banners.FirstOrDefault(b => b.Id == a.BannerImageId.Value).ImageData)
                        : null
                    : null
            });

            return Ok(result);
        }

        [HttpPut("edit-index/{id}")]
        public async Task<IActionResult> EditAnnouncementIndex(int id, [FromBody] int? sortIndex)
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement == null)
                return NotFound(new { message = "Announcement not found." });

            announcement.SortIndex = sortIndex;
            announcement.DateUpdated = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { announcement.AnnouncementId, announcement.SortIndex });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAnnouncement(int id)
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement == null)
                return NotFound(new { message = "Announcement not found." });

            _context.Announcements.Remove(announcement);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Announcement deleted successfully." });
        }
    }
}