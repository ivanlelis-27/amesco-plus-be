namespace AmescoAPI.Models
{
    public class Users
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Mobile { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string MemberId { get; set; } = string.Empty;
        public string? ResetTokenHash { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }
    }
}
