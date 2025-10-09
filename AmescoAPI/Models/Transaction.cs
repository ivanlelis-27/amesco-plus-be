using System;

namespace AmescoAPI.Models
{
    public class Transaction
    {
        public int TransactionId { get; set; }
        public string UserId { get; set; }
        public decimal EarnedPoints { get; set; }
        public DateTime DateIssued { get; set; }
        public int BranchId { get; set; }
    }
}