using System;

namespace AmescoAPI.Models
{
    public class PromoImage
    {
        public int PromoImageId { get; set; }
        public int PromoId { get; set; }
        public byte[] ImageData { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}