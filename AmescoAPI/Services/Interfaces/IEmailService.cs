namespace AmescoAPI.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string htmlBody);
        Task ReadInboxAsync();
    }
}

