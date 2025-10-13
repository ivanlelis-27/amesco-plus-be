namespace AmescoAPI.Models
{
    public class PromoGroup
    {
        public int PromoGroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime DateCreated { get; set; }
    }
}