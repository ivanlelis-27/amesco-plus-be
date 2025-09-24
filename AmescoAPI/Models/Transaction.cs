using System;

namespace AmescoAPI.Models
{
    public class Transaction
    {
        public int TransactionId { get; set; }
        public int UserId { get; set; }
        public decimal EarnedPoints { get; set; }
        public DateTime DateIssued { get; set; }
    }
}