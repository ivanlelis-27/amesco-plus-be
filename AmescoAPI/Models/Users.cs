namespace AmescoAPI.Models
{
    public class Users
    {
        public int UserId { get; set; } // ✅ Matches DB column
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Mobile { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ✅ Navigation properties
        public ICollection<UserSessions>? Sessions { get; set; }
        public Memberships? Membership { get; set; }
    }
}
