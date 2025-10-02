using System;

namespace AmescoAPI.Models
{
    public class BranchPointsHistory
    {
        public int Id { get; set; }
        public int BranchID { get; set; }
        public DateTime Date { get; set; }
        public decimal PointsGiven { get; set; }
    }
}