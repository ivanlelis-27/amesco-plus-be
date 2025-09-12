namespace AmescoAPI.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string htmlBody);
        Task SyncInboxAsync(); // IMAP sync
        Task DownloadInboxAsync(); // POP3 download
    }
}

