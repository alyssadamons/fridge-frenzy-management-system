using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace E_Commerce.Services
{
    public class DummyEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // For now, just log to console (or do nothing)
            Console.WriteLine($"Sending email to {email}: {subject}");
            return Task.CompletedTask;
        }
    }
}
