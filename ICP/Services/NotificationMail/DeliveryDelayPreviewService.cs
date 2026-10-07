using System.Globalization;
using ICP.Data;
using ICP.Helpers;
using ICP.Models.NotificationMail;
using ICP.Models.ShipInfo;
using ICP.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ICP.Services.NotificationMail;

public sealed record DeliveryDelayTodayPreview(bool Enabled, string Frequency,
    IReadOnlyList<string> SendTimes, string MailTo, string CcTo, string Subject,
    string BodyHtml, IReadOnlyList<DeliveryDelayListRow> Rows);

public sealed class DeliveryDelayPreviewService(
    ApplicationDbContext db,
    PageDataScopeService scope,
    UserResourcePermissionService permissions,
    IOptionsMonitor<NotificationMailOptions> options)
{
    public async Task<DeliveryDelayTodayPreview> GetByDelayNotificationDateAsync(
        string delayNotificationDate, CancellationToken cancellationToken)
    {
        if (!permissions.HasPermission(ShipInfoPermissionCodes.Edit))
            throw new ShipInfoForbiddenException("Ship Info Header edit permission is required.");

        if (!DateOnly.TryParseExact(delayNotificationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            throw new ShipInfoBusinessException("Delay Notification Date must use yyyy-MM-dd format.");
        var dashDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var slashDate = date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        var headers = await scope.Apply(db.IcpHeaders).AsNoTracking()
            .Where(h => (h.DelayNotificationDate == dashDate || h.DelayNotificationDate == slashDate)
                && h.ReasonForDeliveryDelay != null && h.ReasonForDeliveryDelay.Trim() != "")
            .OrderBy(h => h.Hawb).ThenBy(h => h.InvoiceNo)
            .ToListAsync(cancellationToken);
        var invoiceNos = headers.Select(h => h.InvoiceNo).Distinct().ToList();
        var details = invoiceNos.Count == 0 ? [] : await scope.ApplyDetails(db.IcpDetails).AsNoTracking()
            .Where(d => invoiceNos.Contains(d.InvoiceNo))
            .OrderBy(d => d.InvoiceNo).ThenBy(d => d.InvoiceSeq)
            .ToListAsync(cancellationToken);
        var current = options.CurrentValue;
        var mail = current.DeliveryDelay;
        var statuses = await DeliveryDelayStatusReader.LoadAsync(db, date, headers,
            mail.Frequency, cancellationToken);
        return new DeliveryDelayTodayPreview(
            mail.Enabled, mail.Frequency, mail.SendTimes,
            string.Join("; ", mail.MailTo), string.Join("; ", mail.CcTo),
            headers.Count == 0 ? string.Empty : DeliveryDelayMailWorker.BuildSubject(mail.Title, headers, date),
            headers.Count == 0 ? string.Empty : DeliveryDelayMailWorker.BuildBody(headers, details, current.ShipInfoUrl, date),
            headers.Select(h => new DeliveryDelayListRow(h.Id, h.InvoiceNo, h.Hawb,
                h.Warehouse, h.Flt, h.Eta, h.ReasonForDeliveryDelay,
                h.DelayNotificationDate, statuses[h.Id])).ToList());
    }
}
