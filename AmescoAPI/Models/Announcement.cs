namespace AmescoAPI.Models
{
    public class Announcement
    {
        public int AnnouncementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? BannerImageId { get; set; }
        public int? PromoGroupId { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}