using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AmescoAPI.Models
{
    public class Voucher
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int VoucherNumber { get; set; }
        public long VoucherId { get; set; }
        public string UserId { get; set; }
        public string VoucherCode { get; set; }
        public decimal Value { get; set; }
        public decimal PointsDeducted { get; set; }
        public DateTime DateCreated { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? DateUsed { get; set; }
    }
}