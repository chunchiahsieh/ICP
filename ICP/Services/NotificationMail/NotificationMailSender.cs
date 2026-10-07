using System.Net;
using System.Net.Mail;
using ICP.Data;
using ICP.Models.Icp;
using ICP.Models.NotificationMail;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services.NotificationMail;

public static class NotificationMailSender
{
    public static async Task SendAsync(ApplicationDbContext db, NotificationSmtpOptions smtp,
        string mailType, string eventKey, IReadOnlyList<string> mailTo, IReadOnlyList<string> ccTo,
        string subject, string bodyHtml, CancellationToken cancellationToken)
    {
        var log = new NotificationMailLog
        {
            Id = Guid.NewGuid(), MailType = mailType, EventKey = eventKey,
            MailTo = string.Join("; ", mailTo), CcTo = string.Join("; ", ccTo),
            Subject = subject, BodyHtml = bodyHtml, State = "Sending", CreatedUtc = DateTime.UtcNow
        };
        db.NotificationMailLogs.Add(log);
        // Never send mail unless its attempt can first be recorded.
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(smtp.FromAddress, smtp.FromName),
                Subject = subject, Body = bodyHtml, IsBodyHtml = true
            };
            foreach (var address in mailTo) message.To.Add(address);
            foreach (var address in ccTo) message.CC.Add(address);
            using var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                EnableSsl = smtp.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };
            if (!string.IsNullOrWhiteSpace(smtp.UserName))
                client.Credentials = new NetworkCredential(smtp.UserName, smtp.Password);
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await db.NotificationMailLogs.Where(item => item.Id == log.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, "Failed")
                    .SetProperty(item => item.ErrorMessage, ex.Message.Length > 2000
                        ? ex.Message.Substring(0, 2000) : ex.Message), cancellationToken);
            throw;
        }

        await db.NotificationMailLogs.Where(item => item.Id == log.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, "Sent")
                .SetProperty(item => item.SentUtc, DateTime.UtcNow), cancellationToken);
    }
}
