using System;

namespace AmescoAPI.Models
{
    public class Promo
    {
        public int PromoId { get; set; }
        public string BrandItemName { get; set; }
        public decimal Price { get; set; }
        public string Unit { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}