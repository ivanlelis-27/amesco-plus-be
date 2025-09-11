using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using AmescoAPI.Models;   // for EmailSettings

namespace AmescoAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> opts)
        {
            _settings = opts.Value;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task ReadInboxAsync()
        {
            using var client = new MailKit.Net.Imap.ImapClient();
            await client.ConnectAsync(_settings.ImapHost, _settings.ImapPort, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass);
            var inbox = client.Inbox;
            await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly);
            var uids = await inbox.SearchAsync(MailKit.Search.SearchQuery.NotSeen);
            foreach (var uid in uids)
            {
                var msg = await inbox.GetMessageAsync(uid);
                // process msg
            }
            await client.DisconnectAsync(true);
        }
    }
}
