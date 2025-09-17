using System;

namespace AmescoAPI.Models
{
    public class Voucher
    {
        public long VoucherId { get; set; }
        public int UserId { get; set; }
        public string VoucherCode { get; set; }
        public decimal Value { get; set; }
        public decimal PointsDeducted { get; set; }
        public DateTime DateCreated { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? DateUsed { get; set; }
    }
}