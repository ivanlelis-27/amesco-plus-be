namespace AmescoAPI.Models
{
    public class UserImage
    {
        public int Id { get; set; } // Primary key
        public string MemberId { get; set; } = null!;
        public byte[] ProfileImage { get; set; } = null!;
        public string ImageType { get; set; } = "png"; // or "jpeg"
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
