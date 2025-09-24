using System;

namespace AmescoAPI.Models
{
    public class Branch
    {
        public int BranchID { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Contact { get; set; }
        public string? Hours { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }
}