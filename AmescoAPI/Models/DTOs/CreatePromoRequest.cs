namespace AmescoAPI.Models.DTOs
{
    public class CreatePromoRequest
    {
        public string BrandItemName { get; set; }
        public decimal Price { get; set; }
        public string Unit { get; set; }
        public IFormFile Image { get; set; }
    }
}