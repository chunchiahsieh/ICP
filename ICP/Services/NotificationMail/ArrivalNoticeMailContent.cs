using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using ICP.Data;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.ShipInfo;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services.NotificationMail;

public sealed record ArrivalNoticeMailGroup(
    IReadOnlyList<ArrivalNoticeSchedule> Schedules,
    IReadOnlyList<IcpHeader> Headers,
    IReadOnlyList<string> MailTo,
    string RecipientKey,
    DateTime ScheduledAtUtc);

public sealed record ArrivalNoticeMailSnapshot(
    IReadOnlyList<ArrivalNoticeMailGroup> Groups,
    IReadOnlyList<string> InvalidInvoiceNos);

public static class ArrivalNoticeMailContent
{
    public static async Task<ArrivalNoticeMailSnapshot> LoadScheduledAsync(
        ApplicationDbContext db, DateTime? fromUtc, DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var scheduleQuery = db.ArrivalNoticeSchedules.AsNoTracking().Where(row => row.State == "Pending");
        if (fromUtc.HasValue) scheduleQuery = scheduleQuery.Where(row => row.ScheduledAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) scheduleQuery = scheduleQuery.Where(row => row.ScheduledAtUtc <= toUtc.Value);
        var schedules = await scheduleQuery.OrderBy(row => row.ScheduledAtUtc)
            .ThenBy(row => row.InvoiceNo).ToListAsync(cancellationToken);
        var headerIds = schedules.Select(row => row.HeaderId).Distinct().ToList();
        var headers = await db.IcpHeaders.AsNoTracking().Where(h => headerIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, cancellationToken);
        var valid = new List<(ArrivalNoticeSchedule Schedule, IcpHeader Header,
            IReadOnlyList<string> MailTo, string Key)>();
        var invalid = new List<string>();
        foreach (var schedule in schedules)
        {
            if (!headers.TryGetValue(schedule.HeaderId, out var header)
                || !string.Equals(header.ArrivalNotice, schedule.ArrivalNoticeValue, StringComparison.Ordinal)
                || !ShipInfoStatusRules.Resolve(ShipInfoStatusResolver.Resolve(header)).Edit)
            {
                invalid.Add(schedule.InvoiceNo);
                continue;
            }
            var addresses = ParseMailTo(header.ArrivalNotice);
            if (addresses.Count == 0)
            {
                invalid.Add(schedule.InvoiceNo);
                continue;
            }
            valid.Add((schedule, header, addresses, schedule.RecipientKey));
        }
        var groups = valid.GroupBy(item => (item.Key, item.Schedule.ScheduledAtUtc))
            .Select(group => new ArrivalNoticeMailGroup(
                group.Select(item => item.Schedule).ToList(),
                group.Select(item => item.Header).OrderBy(h => h.InvoiceNo, StringComparer.OrdinalIgnoreCase).ToList(),
                group.First().MailTo,
                group.Key.Key,
                group.Key.ScheduledAtUtc))
            .OrderBy(group => group.ScheduledAtUtc)
            .ThenBy(group => group.RecipientKey, StringComparer.OrdinalIgnoreCase).ToList();
        return new ArrivalNoticeMailSnapshot(groups, invalid);
    }

    public static IReadOnlyList<string> ParseMailTo(string? value)
    {
        return TryParseMailTo(value, out var addresses, out _) ? addresses : [];
    }

    public static bool TryParseMailTo(string? value, out IReadOnlyList<string> addresses, out string error)
    {
        addresses = [];
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Arrival Notice is empty. Save at least one email address first.";
            return false;
        }
        if (value.TrimStart().StartsWith('*'))
        {
            error = "Arrival Notice starts with *. Remove the legacy marker before scheduling.";
            return false;
        }
        if (value.Trim().Length >= 300)
        {
            error = "Arrival Notice is too long (maximum 299 characters).";
            return false;
        }
        var text = value.Trim();
        if (text.StartsWith("mail:", StringComparison.OrdinalIgnoreCase)) text = text[5..];
        if (text.Contains(',') || text.Contains('\r') || text.Contains('\n'))
        {
            error = "Separate multiple Arrival Notice email addresses with semicolons (;), not commas or line breaks.";
            return false;
        }
        var parts = text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            error = "Arrival Notice is empty. Save at least one email address first.";
            return false;
        }
        var result = new List<string>();
        for (var index = 0; index < parts.Length; index++)
        {
            var address = parts[index];
            if (!MailAddress.TryCreate(address, out var parsed)
                || !string.Equals(parsed.Address, address, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Arrival Notice email #{index + 1} is invalid. Use addresses like mail:a@tel.com;b@tel.com.";
                return false;
            }
            if (!result.Contains(parsed.Address, StringComparer.OrdinalIgnoreCase)) result.Add(parsed.Address);
        }
        addresses = result.OrderBy(address => address, StringComparer.OrdinalIgnoreCase).ToList();
        return true;
    }

    public static string BuildEventKey(ArrivalNoticeMailGroup group)
    {
        var source = group.RecipientKey + "|" + string.Join("|", group.Schedules
            .OrderBy(row => row.Id).Select(row => row.Id.ToString("N")));
        return "arrival-notice:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    public static string BuildSubject(string configuredTitle) =>
        string.IsNullOrWhiteSpace(configuredTitle) ? "【Arrival notice】" : configuredTitle.Trim();

    public static async Task<string> BuildBodyAsync(ApplicationDbContext db,
        IReadOnlyList<IcpHeader> headers, string shipInfoUrl, CancellationToken cancellationToken)
    {
        static string E(object? value) => HtmlEncoder.Default.Encode(
            Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        const string tableStyle = "border-collapse:collapse; font-family:Arial,sans-serif; font-size:13px; margin-top:4px;";
        const string headerStyle = "border:1px solid #000; background:#f2f2f2; padding:5px 7px; text-align:center; font-weight:bold;";
        const string cellStyle = "border:1px solid #000; padding:5px 7px; vertical-align:top;";
        static void Cell(StringBuilder html, object? value) => html.Append("<td style=\"").Append(cellStyle)
            .Append("\">").Append(E(value)).Append("</td>");

        var invoiceNos = headers.Select(h => h.InvoiceNo).Distinct().ToList();
        var details = await db.IcpDetails.AsNoTracking()
            .Where(d => invoiceNos.Contains(d.InvoiceNo))
            .OrderBy(d => d.InvoiceNo).ThenBy(d => d.InvoiceSeq)
            .ToListAsync(cancellationToken);
        var deliveryKeys = headers.Select(h => h.DeliveryTo).Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct().ToList();
        var deliveryNames = await db.SystemConfigs.AsNoTracking()
            .Where(config => !config.IsDeleted && config.Category == "DeliveryToList"
                && deliveryKeys.Contains(config.Key1))
            .ToDictionaryAsync(config => config.Key1,
                config => config.Value1 ?? config.Key1, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var html = new StringBuilder("<div style=\"font-family:Arial,sans-serif;font-size:14px;color:#000;\">")
            .Append("<p style=\"margin:0;\">Dear,</p>")
            .Append("<p style=\"margin:0;\">Please refer to below arrival notice.</p>");
        if (Uri.TryCreate(shipInfoUrl, UriKind.Absolute, out var url)
            && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp))
        {
            var destination = new UriBuilder(url);
            var invoiceNo = headers.FirstOrDefault()?.InvoiceNo;
            if (!string.IsNullOrWhiteSpace(invoiceNo))
            {
                var existingQuery = destination.Query.TrimStart('?');
                destination.Query = (existingQuery.Length == 0 ? string.Empty : existingQuery + "&")
                    + "InvoiceNo=" + Uri.EscapeDataString(invoiceNo);
            }
            html.Append("<p style=\"margin:0 0 4px 0;\"><a href=\"").Append(E(destination.Uri.AbsoluteUri))
                .Append("\">Delivery Notification System</a></p>");
        }
        html.Append("<table style=\"").Append(tableStyle).Append("\"><thead><tr>");
        foreach (var label in new[] { "Del Date", "Del Time", "Del. to", "BU", "Receiver", "Owner",
                     "InvNo", "NCDR No.", "Customer", "Machine No", "ITEM", "DESCRIPTION", "QTY",
                     "Hwab", "FlightNo", "ETA", "PO", "Length", "Width", "Height", "Weight",
                     "Packing Type", "WBS Element" })
            html.Append("<th style=\"").Append(headerStyle).Append("\">").Append(E(label)).Append("</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var header in headers.OrderBy(h => h.InvoiceNo, StringComparer.OrdinalIgnoreCase))
        {
            var rows = details.Where(d => d.InvoiceNo == header.InvoiceNo && d.TetPo == header.TetPo)
                .OrderBy(d => d.InvoiceSeq).ToList();
            if (rows.Count == 0) rows.Add(new IcpDetail());
            foreach (var detail in rows)
            {
                html.Append("<tr>");
                Cell(html, header.DeliveryDate); Cell(html, header.ArriveTime);
                Cell(html, header.DeliveryTo is not null
                    && deliveryNames.TryGetValue(header.DeliveryTo, out var name) ? name : header.DeliveryTo);
                Cell(html, header.Bu); Cell(html, header.Receiver); Cell(html, header.Owner);
                Cell(html, header.InvoiceNo); Cell(html, header.NcdrNo); Cell(html, header.EndUser);
                Cell(html, header.MachineNo); Cell(html, detail.ItemNo); Cell(html, detail.Description);
                Cell(html, detail.Qty); Cell(html, header.Hawb); Cell(html, header.Flt);
                Cell(html, header.Eta); Cell(html, header.TetPo); Cell(html, detail.Length);
                Cell(html, detail.Width); Cell(html, detail.Hight); Cell(html, detail.GrossWeight);
                Cell(html, detail.PackingType); Cell(html, header.WbsElement);
                html.Append("</tr>");
            }
        }
        return html.Append("</tbody></table></div>").ToString();
    }
}
