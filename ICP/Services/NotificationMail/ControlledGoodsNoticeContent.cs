using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using ICP.Data;
using ICP.Models.Icp;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services.NotificationMail;

public static class ControlledGoodsNoticeContent
{
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);

    public static bool TryScheduleUtc(string? eta, DateTime nowUtc,
        IReadOnlyList<string> sendTimes, out DateTime scheduledUtc)
    {
        scheduledUtc = default;
        if (!DateOnly.TryParseExact(eta, ["yyyy-MM-dd", "yyyy/MM/dd"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return false;
        var slots = sendTimes.Select(text => TimeOnly.TryParseExact(text, "HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : (TimeOnly?)null)
            .Where(time => time.HasValue).Select(time => time!.Value).Order().ToList();
        if (slots.Count == 0) return false;
        var slot = slots[0];
        scheduledUtc = new DateTimeOffset(date.Year, date.Month, date.Day,
            slot.Hour, slot.Minute, 0, TaipeiOffset).UtcDateTime;
        return scheduledUtc > nowUtc;
    }

    public static string BuildSubject(string configuredTitle, string? eta)
    {
        var title = string.IsNullOrWhiteSpace(configuredTitle)
            ? "【通知】管制品到貨 - ETA:"
            : configuredTitle.Trim();
        var date = DateOnly.ParseExact(eta ?? string.Empty, ["yyyy-MM-dd", "yyyy/MM/dd"],
            CultureInfo.InvariantCulture, DateTimeStyles.None);
        return title + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static async Task<string> BuildBodyAsync(ApplicationDbContext db, IcpHeader header,
        string shipInfoUrl, CancellationToken cancellationToken)
    {
        static string E(object? value) => HtmlEncoder.Default.Encode(
            Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
        const string table = "border-collapse:collapse;font-family:Arial,sans-serif;font-size:13px;margin:8px 0 16px";
        const string heading = "border:1px solid #333;background:#f2f2f2;padding:5px 7px;text-align:center;font-weight:bold";
        const string cell = "border:1px solid #333;padding:5px 7px;vertical-align:top";
        var details = await db.IcpDetails.AsNoTracking()
            .Where(d => d.InvoiceNo == header.InvoiceNo && d.TetPo == header.TetPo
                && d.ElFlag != null && d.ElFlag.Trim() != "")
            .OrderBy(d => d.InvoiceSeq).ToListAsync(cancellationToken);
        var deliveryTo = header.DeliveryTo;
        if (!string.IsNullOrWhiteSpace(deliveryTo))
        {
            var configuredName = await db.SystemConfigs.AsNoTracking()
                .Where(config => !config.IsDeleted && config.Category == "DeliveryToList"
                    && config.Key1 == deliveryTo)
                .Select(config => config.Value1).FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(configuredName)) deliveryTo = configuredName;
        }
        var html = new StringBuilder("<div style=\"font-family:Arial,sans-serif;font-size:14px\">")
            .Append("<p>管制品到貨通知</p>");
        if (Uri.TryCreate(shipInfoUrl, UriKind.Absolute, out var url)
            && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp))
        {
            var separator = string.IsNullOrEmpty(url.Query) ? "?" : "&";
            html.Append("<p><a href=\"").Append(E(url.AbsoluteUri + separator
                + "InvoiceNo=" + Uri.EscapeDataString(header.InvoiceNo)))
                .Append("\">Delivery Notification System</a></p>");
        }
        html.Append("<table style=\"").Append(table).Append("\"><thead><tr>");
        foreach (var label in new[] { "Invoice No", "ETA", "MAWB", "HAWB", "FLT", "W/H",
                     "Order Type", "Delivery Date", "Delivery To", "BU", "TET PO", "PO Line",
                     "Invoice Seq", "Item No", "Description", "QTY",
                     "ILSTW: Customer / ICP: End User", "Sold-to Party", "Ship-to Party", "ECCN" })
            html.Append("<th style=\"").Append(heading).Append("\">").Append(E(label)).Append("</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var detail in details)
        {
            html.Append("<tr>");
            foreach (var value in new object?[] { header.InvoiceNo, header.Eta, header.Mawb,
                         header.Hawb, header.Flt, header.Warehouse, header.OrderType,
                         header.DeliveryDate, deliveryTo, header.Bu, header.TetPo,
                         detail.TetPoLine, detail.InvoiceSeq, detail.ItemNo, detail.Description,
                         detail.Qty, header.EndUser, header.SoldToParty, header.ShipToParty,
                         detail.Eccn })
                html.Append("<td style=\"").Append(cell).Append("\">").Append(E(value)).Append("</td>");
            html.Append("</tr>");
        }
        return html.Append("</tbody></table></div>").ToString();
    }
}
