namespace AmescoAPI.Models
{
    public class UserSessions
    {
        public string SessionId { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string? JwtToken { get; set; }

        public Users? User { get; set; }
    }
}
