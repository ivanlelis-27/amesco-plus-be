using System;

namespace AmescoAPI.Models
{
    public class NotificationLike
    {
        public int LikeId { get; set; }
        public int NotificationId { get; set; }
        public string UserId { get; set; } // <-- Use UserId instead of MemberId
        public DateTime LikedAt { get; set; }
    }
}