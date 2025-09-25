using System;

namespace AmescoAPI.Models
{
    public class Branch
    {
        public int BranchID { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Contact { get; set; }
        public string? Email { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string? StartDay { get; set; }
        public string? EndDay { get; set; }
        public TimeSpan? OpenTime { get; set; }
        public TimeSpan? CloseTime { get; set; }
    }
}