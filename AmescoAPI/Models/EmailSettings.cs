namespace AmescoAPI.Models
{
    public class EmailSettings
    {
        public required string SenderName { get; set; }
        public required string SenderEmail { get; set; }
        public required string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public required string ImapHost { get; set; }
        public int ImapPort { get; set; }
        public required string SmtpUser { get; set; }
        public required string SmtpPass { get; set; }
    }
}
