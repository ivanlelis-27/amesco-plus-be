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
            [FromForm] List<int> promoIds 
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

            // loads all banners in one query and creates a lookup
            var banners = _imagesDb.AdBanners
                .Where(b => bannerIds.Contains(b.Id))
                .ToDictionary(b => b.Id, b => b.ImageData);

            // loads all announcement products in one query and groups by announcement
            var announcementIds = announcements.Select(a => a.AnnouncementId).ToList();
            var announcementProducts = _context.AnnouncementProducts
                .Where(ap => announcementIds.Contains(ap.AnnouncementId))
                .GroupBy(ap => ap.AnnouncementId)
                .ToDictionary(g => g.Key, g => g.Select(ap => ap.PromoId).ToList());

            var result = announcements.Select(a => new
            {
                a.AnnouncementId,
                a.Title,
                a.Description,
                a.PromoGroupId,
                a.BannerImageId,
                a.DateCreated,
                a.DateUpdated,
                a.SortIndex,
                imageBase64 = (a.BannerImageId.HasValue && banners.ContainsKey(a.BannerImageId.Value))
                    ? Convert.ToBase64String(banners[a.BannerImageId.Value])
                    : null,
                promoIds = announcementProducts.ContainsKey(a.AnnouncementId)
                    ? announcementProducts[a.AnnouncementId]
                    : new List<int>()
            });

            return Ok(result);
        }

        [HttpGet("{id}")]
        public IActionResult GetAnnouncementById(int id)
        {
            var announcement = _context.Announcements.FirstOrDefault(a => a.AnnouncementId == id);
            if (announcement == null)
                return NotFound(new { message = "Announcement not found." });

            var banner = announcement.BannerImageId.HasValue
                ? _imagesDb.AdBanners.FirstOrDefault(b => b.Id == announcement.BannerImageId.Value)
                : null;

            var promoIds = _context.AnnouncementProducts
                .Where(ap => ap.AnnouncementId == id)
                .Select(ap => ap.PromoId)
                .ToList();

            return Ok(new
            {
                announcement.AnnouncementId,
                announcement.Title,
                announcement.Description,
                announcement.PromoGroupId,
                announcement.BannerImageId,
                announcement.DateCreated,
                announcement.DateUpdated,
                announcement.SortIndex,
                imageBase64 = banner?.ImageData != null ? Convert.ToBase64String(banner.ImageData) : null,
                promoIds
            });
        }

        [HttpGet("{id}/promos")]
        public IActionResult GetAnnouncementPromos(int id)
        {
            var announcement = _context.Announcements.FirstOrDefault(a => a.AnnouncementId == id);
            if (announcement == null)
                return NotFound(new { message = "Announcement not found." });

            var promoGroupName = announcement.PromoGroupId.HasValue
                ? _context.PromoGroups.FirstOrDefault(pg => pg.PromoGroupId == announcement.PromoGroupId.Value)?.Name
                : null;

            var promoIds = _context.AnnouncementProducts
                .Where(ap => ap.AnnouncementId == id)
                .Select(ap => ap.PromoId)
                .ToList();

            var promos = _context.Promos
                .Where(p => promoIds.Contains(p.PromoId))
                .ToList();

            var promoImages = _imagesDb.PromoImages
                .Where(pi => promoIds.Contains(pi.PromoId))
                .ToList();

            var result = promos.Select(p => new
            {
                p.PromoId,
                brandItemName = p.BrandItemName,
                price = p.Price,
                unit = p.Unit,
                imageBase64 = promoImages.FirstOrDefault(pi => pi.PromoId == p.PromoId)?.ImageData != null
                    ? Convert.ToBase64String(promoImages.FirstOrDefault(pi => pi.PromoId == p.PromoId).ImageData)
                    : null,
                promoGroupName
            }).ToList();

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