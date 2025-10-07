using System;

namespace AmescoAPI.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string MessageBody { get; set; }
        public DateTime ScheduledAt { get; set; }
        public bool IncludeImage { get; set; }
        public DateTime CreatedAt { get; set; }

        public int LikeCount { get; set; }
    }
}