using System;

namespace AmescoAPI.Models
{
    public class ExistingMember
    {
        public int Id { get; set; }
        public string MemberId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MobileNumber { get; set; }
        public DateTime ImportedAt { get; set; }
    }
}