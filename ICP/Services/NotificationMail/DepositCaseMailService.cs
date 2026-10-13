using ICP.Data;
using ICP.Models.Icp;
using ICP.Models.NotificationMail;
using ICP.Repositories;
using Microsoft.Extensions.Options;
namespace ICP.Services.NotificationMail;

public sealed class DepositCaseMailService(ApplicationDbContext db, IOptionsMonitor<NotificationMailOptions> options,
    ILogger<DepositCaseMailService> logger)
{
    public object Preview(Dictionary<string, object?> header, IEnumerable<Dictionary<string, object?>> details)
    {
        var mail = options.CurrentValue.DepositCase;
        return new { mailTo = string.Join("; ", mail.MailTo), ccTo = string.Join("; ", mail.CcTo),
            subject = DepositCaseMailContent.BuildSubject(Convert.ToString(header.GetValueOrDefault("InvoiceNo")) ?? ""),
            bodyHtml = DepositCaseMailContent.BuildBody(header, details) };
    }
    public async Task SendAsync(IcpHeader header, IReadOnlyList<IcpDetail> details, string caseNo, CancellationToken ct)
    {
        var current = options.CurrentValue;
        if (!current.DepositCase.Enabled) return;
        try
        {
            await NotificationMailSender.SendAsync(db, current.Smtp, "DepositCase", $"deposit:{header.Id:N}:{caseNo}",
                current.DepositCase.MailTo, current.DepositCase.CcTo,
                DepositCaseMailContent.BuildSubject(header.InvoiceNo),
                DepositCaseMailContent.BuildBody(ShipInfoEntityMapper.MapEntity(header), details.Select(ShipInfoEntityMapper.MapEntity)), ct);
        }
        catch (Exception ex) { logger.LogError(ex, "Deposit case notification failed for {InvoiceNo}; case remains initiated", header.InvoiceNo); }
    }
}
