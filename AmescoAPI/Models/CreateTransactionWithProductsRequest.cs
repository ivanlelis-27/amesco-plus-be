using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AmescoAPI.Models
{
    public class CreateTransactionWithProductsRequest
    {
        [Required]
        public string UserId { get; set; }
        [Required]
        public decimal EarnedPoints { get; set; }
        public List<ProductDto> Products { get; set; } = new();
        public int BranchId { get; set; }
    }

    public class ProductDto
    {
        [Required]
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
    }
}