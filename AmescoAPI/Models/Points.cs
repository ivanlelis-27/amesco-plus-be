namespace AmescoAPI.Models
{
    public class Points
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public decimal PointsBalance { get; set; } = 0;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}