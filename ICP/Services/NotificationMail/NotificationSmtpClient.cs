using System.Net;
using System.Net.Mail;
using ICP.Models.NotificationMail;

namespace ICP.Services.NotificationMail;

/// <summary>Shared SMTP connection and delivery for all notification types.</summary>
public static class NotificationSmtpClient
{
    public static SmtpClient CreateClient(NotificationSmtpOptions options)
    {
        var client = new SmtpClient(options.Host)
        {
            Port = options.Port,
            EnableSsl = options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrWhiteSpace(options.UserName))
            client.Credentials = new NetworkCredential(options.UserName, options.Password);
        return client;
    }

    public static async Task SendAsync(NotificationSmtpOptions options, MailMessage message,
        CancellationToken cancellationToken = default)
    {
        message.From = new MailAddress(options.FromAddress, options.FromName);
        using var client = CreateClient(options);
        await client.SendMailAsync(message, cancellationToken);
    }
}
