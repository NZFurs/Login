using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NZFurs.Auth.Services
{
    public class FakeEmailService : IEmailSender
    {
        private readonly ILogger<FakeEmailService> _logger;

        public FakeEmailService(ILogger<FakeEmailService> logger)
        {
            _logger = logger;
        }
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            _logger.LogInformation($"Email send triggered:\n\nTo: {email}\nSubject: {subject}\nBody:\n{htmlMessage}");
            return Task.CompletedTask;
        }
    }
}
