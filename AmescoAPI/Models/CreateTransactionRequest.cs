using System.ComponentModel.DataAnnotations;

namespace AmescoAPI.Models
{
    public class CreateTransactionRequest
    {
        [Required]
        public string UserId { get; set; }
        [Required]
        public int EarnedPoints { get; set; }
        
    }
}