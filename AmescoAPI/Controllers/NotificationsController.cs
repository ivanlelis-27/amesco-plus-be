using AmescoAPI.Data;
using AmescoAPI.Models;
using AmescoAPI.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _db;

        private readonly ImagesDbContext _imagesDb;

        public NotificationsController(AppDbContext db, ImagesDbContext imagesDb)
        {
            _db = db;
            _imagesDb = imagesDb;
        }

        [HttpPost]
        [Route("create")]
        public async Task<IActionResult> CreateNotification([FromForm] CreateNotificationRequest request)
        {
            if (request.IncludeImage)
            {
                if (request.Image == null || request.Image.Length == 0)
                    return BadRequest("Image is required when IncludeImage is true.");
            }

            var notification = new Notification
            {
                Title = request.Title,
                Description = request.Description,
                MessageBody = request.MessageBody,
                ScheduledAt = request.ScheduledAt,
                IncludeImage = request.IncludeImage,
                CreatedAt = DateTime.UtcNow
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            // Only save image if provided and IncludeImage is true
            if (request.IncludeImage && request.Image != null && request.Image.Length > 0)
            {
                using var ms = new MemoryStream();
                await request.Image.CopyToAsync(ms);

                var notificationImage = new NotificationImage
                {
                    NotificationId = notification.NotificationId,
                    ImageData = ms.ToArray(),
                    UploadedAt = DateTime.UtcNow
                };

                _imagesDb.NotificationImages.Add(notificationImage);
                await _imagesDb.SaveChangesAsync();
            }

            return Ok(new { notification.NotificationId });
        }

        [HttpGet]
        [Route("list")]
        public async Task<IActionResult> GetNotifications()
        {
            var notifications = await _db.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return Ok(notifications);
        }
    }
}