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

        [HttpPost]
        [Route("like")]
        public async Task<IActionResult> LikeNotification(int notificationId, string userId)
        {
            var existingLike = await _db.NotificationLikes
                .FirstOrDefaultAsync(l => l.NotificationId == notificationId && l.UserId == userId);

            if (existingLike != null)
                return BadRequest("Already liked.");

            var like = new NotificationLike
            {
                NotificationId = notificationId,
                UserId = userId,
                LikedAt = DateTime.UtcNow
            };
            _db.NotificationLikes.Add(like);

            var notification = await _db.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.LikeCount += 1;
                await _db.SaveChangesAsync();
            }

            return Ok(new { notificationId, userId });
        }

        [HttpPost]
        [Route("unlike")]
        public async Task<IActionResult> UnlikeNotification(int notificationId, string userId)
        {
            var existingLike = await _db.NotificationLikes
                .FirstOrDefaultAsync(l => l.NotificationId == notificationId && l.UserId == userId);

            if (existingLike == null)
                return BadRequest("Like does not exist.");

            _db.NotificationLikes.Remove(existingLike);

            var notification = await _db.Notifications.FindAsync(notificationId);
            if (notification != null && notification.LikeCount > 0)
            {
                notification.LikeCount -= 1;
                await _db.SaveChangesAsync();
            }

            return Ok(new { notificationId, userId });
        }

        [HttpGet("most-liked")]
        public async Task<IActionResult> GetMostLikedNotification()
        {
            var notification = await _db.Notifications
                .OrderByDescending(n => n.LikeCount)
                .FirstOrDefaultAsync();

            if (notification == null)
                return NotFound("No notifications found.");

            var image = await _imagesDb.NotificationImages
                .FirstOrDefaultAsync(img => img.NotificationId == notification.NotificationId);

            return Ok(new
            {
                notification.NotificationId,
                notification.Title,
                notification.Description,
                notification.MessageBody,
                notification.ScheduledAt,
                notification.IncludeImage,
                notification.CreatedAt,
                notification.LikeCount,
                ImageBase64 = image?.ImageData != null ? Convert.ToBase64String(image.ImageData) : null
            });
        }

        [HttpGet]
        [Route("list")]
        public async Task<IActionResult> GetNotifications(string userId)
        {
            var notifications = await _db.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            var notificationIds = notifications.Select(n => n.NotificationId).ToList();
            var images = await _imagesDb.NotificationImages
                .Where(img => notificationIds.Contains(img.NotificationId))
                .ToListAsync();

            var likedIds = await _db.NotificationLikes
                .Where(l => l.UserId == userId)
                .Select(l => l.NotificationId)
                .ToListAsync();

            var result = notifications.Select(n => new
            {
                n.NotificationId,
                n.Title,
                n.Description,
                n.MessageBody,
                n.ScheduledAt,
                n.IncludeImage,
                n.CreatedAt,
                n.LikeCount,
                Liked = likedIds.Contains(n.NotificationId),
                ImageBase64 = images.FirstOrDefault(img => img.NotificationId == n.NotificationId)?.ImageData != null
                    ? Convert.ToBase64String(images.FirstOrDefault(img => img.NotificationId == n.NotificationId).ImageData)
                    : null
            });

            return Ok(result);
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllNotifications()
        {
            var notifications = await _db.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            var notificationIds = notifications.Select(n => n.NotificationId).ToList();
            var images = await _imagesDb.NotificationImages
                .Where(img => notificationIds.Contains(img.NotificationId))
                .ToListAsync();

            var result = notifications.Select(n => new
            {
                n.NotificationId,
                n.Title,
                n.Description,
                n.MessageBody,
                n.ScheduledAt,
                n.IncludeImage,
                n.CreatedAt,
                n.LikeCount,
                ImageBase64 = images.FirstOrDefault(img => img.NotificationId == n.NotificationId)?.ImageData != null
                    ? Convert.ToBase64String(images.FirstOrDefault(img => img.NotificationId == n.NotificationId).ImageData)
                    : null
            });

            return Ok(result);
        }

        [HttpPut]
        [Route("edit/{notificationId}")]
        public async Task<IActionResult> EditScheduledNotification(int notificationId, [FromForm] CreateNotificationRequest request)
        {
            var notification = await _db.Notifications.FindAsync(notificationId);
            if (notification == null)
                return NotFound("Notification not found.");

            // Only allow editing if scheduled time is in the future
            if (notification.ScheduledAt <= DateTime.UtcNow)
                return BadRequest("Cannot edit notifications that are already sent or in history.");

            notification.Title = request.Title;
            notification.Description = request.Description;
            notification.MessageBody = request.MessageBody;
            notification.ScheduledAt = request.ScheduledAt;
            notification.IncludeImage = request.IncludeImage;

            // If image is included and provided, update or add image
            if (request.IncludeImage && request.Image != null && request.Image.Length > 0)
            {
                var existingImage = await _imagesDb.NotificationImages
                    .FirstOrDefaultAsync(img => img.NotificationId == notificationId);

                using var ms = new MemoryStream();
                await request.Image.CopyToAsync(ms);

                if (existingImage != null)
                {
                    existingImage.ImageData = ms.ToArray();
                    existingImage.UploadedAt = DateTime.UtcNow;
                }
                else
                {
                    var newImage = new NotificationImage
                    {
                        NotificationId = notificationId,
                        ImageData = ms.ToArray(),
                        UploadedAt = DateTime.UtcNow
                    };
                    _imagesDb.NotificationImages.Add(newImage);
                }
            }

            await _db.SaveChangesAsync();
            await _imagesDb.SaveChangesAsync();

            return Ok(new { message = "Notification updated successfully." });
        }

        [HttpDelete]
        [Route("delete/{notificationId}")]
        public async Task<IActionResult> DeleteNotification(int notificationId)
        {
            // Find notification
            var notification = await _db.Notifications.FindAsync(notificationId);
            if (notification == null)
                return NotFound("Notification not found.");

            // Remove associated likes
            var likes = _db.NotificationLikes.Where(l => l.NotificationId == notificationId);
            _db.NotificationLikes.RemoveRange(likes);

            // Remove associated images
            var images = _imagesDb.NotificationImages.Where(img => img.NotificationId == notificationId);
            _imagesDb.NotificationImages.RemoveRange(images);

            // Remove notification
            _db.Notifications.Remove(notification);

            await _db.SaveChangesAsync();
            await _imagesDb.SaveChangesAsync();

            return Ok(new { message = "Notification deleted successfully." });
        }
    }
}