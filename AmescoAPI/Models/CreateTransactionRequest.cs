using System.ComponentModel.DataAnnotations;

namespace AmescoAPI.Models
{
    public class CreateTransactionRequest
    {
        [Required]
        public int UserId { get; set; }
        [Required]
        public int EarnedPoints { get; set; }
    }
}