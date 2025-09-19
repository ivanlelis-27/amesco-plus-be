using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdBannersController : ControllerBase
    {
        private readonly ImagesDbContext _imagesContext;
        public AdBannersController(ImagesDbContext imagesContext)
        {
            _imagesContext = imagesContext;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var banners = _imagesContext.AdBanners.ToList();
            return Ok(banners);
        }

        [HttpPost]
        public IActionResult Create([FromForm] IFormFile image)
        {
            if (image == null)
                return BadRequest("No image uploaded.");

            using var ms = new MemoryStream();
            image.CopyTo(ms);

            var banner = new AdBanner
            {
                FileName = image.FileName,
                ContentType = image.ContentType,
                ImageData = ms.ToArray(),
                CreatedAt = DateTime.UtcNow
            };

            _imagesContext.AdBanners.Add(banner);
            _imagesContext.SaveChanges();

            return Ok(banner);
        }
    }
}