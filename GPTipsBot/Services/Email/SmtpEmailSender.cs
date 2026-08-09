using System.Net;
using System.Net.Mail;
using GPTipsBot.Config;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Services.Email;

public class SmtpEmailSender(ILogger<SmtpEmailSender> logger)
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        if (!SmtpConfig.IsEnabled)
        {
            logger.LogWarning("SMTP is not configured; email to {To} was not sent. Subject={Subject}", to, subject);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(SmtpConfig.From!),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        message.To.Add(to);

        using var client = new SmtpClient(SmtpConfig.Host, SmtpConfig.Port)
        {
            EnableSsl = SmtpConfig.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        if (!string.IsNullOrWhiteSpace(SmtpConfig.User))
        {
            client.Credentials = new NetworkCredential(SmtpConfig.User, SmtpConfig.Password ?? "");
        }

        await client.SendMailAsync(message, cancellationToken);
        logger.LogInformation("Email sent to {To} subject={Subject}", to, subject);
    }
}
