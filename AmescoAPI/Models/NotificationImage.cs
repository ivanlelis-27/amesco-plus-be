using System;

namespace AmescoAPI.Models
{
    public class NotificationImage
    {
        public int ImageId { get; set; }
        public int NotificationId { get; set; }
        public byte[] ImageData { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}