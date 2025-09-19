using System;

namespace AmescoAPI.Models
{
    public class AdBanner
    {
        public int Id { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public byte[] ImageData { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}