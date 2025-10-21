namespace AmescoAPI.Models
{
    public class Memberships
    {
        public string MemberId { get; set; } = string.Empty;
        public int UserId { get; set; }

        public Users? User { get; set; }
    }
}
