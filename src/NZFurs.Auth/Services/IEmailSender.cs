using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NZFurs.Auth.Services
{
    public interface IEmailSender : Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string htmlMessage, string toUsername);
    }
}
