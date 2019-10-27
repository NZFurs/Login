using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NZFurs.Auth.Models;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace NZFurs.Auth.Services
{
    public class SendGridEmailSender : IEmailSender
    {
        private readonly IOptions<SendGridOptions> _optionsEmailSettings;

        public SendGridEmailSender(IOptions<SendGridOptions> optionsEmailSettings)
        {
            _optionsEmailSettings = optionsEmailSettings;
        }

        public async Task SendEmailAsync(string email, string subject, string message, string toUsername)
        {
            var client = new SendGridClient(_optionsEmailSettings.Value.ApiKey);
            var msg = new SendGridMessage();
            msg.SetFrom(new EmailAddress(_optionsEmailSettings.Value.SenderEmailAddress, "NZFurs"));
            msg.AddTo(new EmailAddress(email, toUsername));
            msg.SetSubject(subject);
            //msg.AddContent(MimeType.Text, message);
            msg.AddContent(MimeType.Html, message);

            msg.SetReplyTo(new EmailAddress(_optionsEmailSettings.Value.SenderEmailAddress, "NZFurs"));
            
            var response = await client.SendEmailAsync(msg);
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            return SendEmailAsync(email, subject, htmlMessage, null);
        }
    }
}
