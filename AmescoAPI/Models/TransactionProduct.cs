using System.ComponentModel.DataAnnotations;

namespace AmescoAPI.Models
{
    public class TransactionProduct
    {
        public int TransactionProductId { get; set; }
        public int TransactionId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
    }
}