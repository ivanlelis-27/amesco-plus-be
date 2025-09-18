using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AmescoAPI.Models
{
    public class CreateTransactionWithProductsRequest
    {
        [Required]
        public int UserId { get; set; }
        [Required]
        public int EarnedPoints { get; set; }
        public List<ProductDto> Products { get; set; } = new();
    }

    public class ProductDto
    {
        [Required]
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
    }
}