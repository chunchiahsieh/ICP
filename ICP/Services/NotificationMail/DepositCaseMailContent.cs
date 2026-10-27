using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

namespace ICP.Services.NotificationMail;

public static class DepositCaseMailContent
{
    public static string BuildSubject(string invoiceNo) => "Deposit 起案通知 - Invoice#" + invoiceNo;
    public static string BuildBody(IReadOnlyDictionary<string, object?> header,
        IEnumerable<IReadOnlyDictionary<string, object?>> details)
    {
        static string E(object? value) => HtmlEncoder.Default.Encode(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
        object? H(string key) => header.TryGetValue(key, out var value) ? value : null;
        var html = new StringBuilder("<p>起案通知。</p><table style=\"border-collapse:collapse;font-family:Arial,sans-serif\"><thead><tr>");
        foreach (var label in new[] { "Customer", "MAWB", "HAWB", "Invoice No", "Flight", "Item No", "Description", "QTY" })
            html.Append("<th style=\"border:1px solid #333;padding:5px\">").Append(E(label)).Append("</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var detail in details)
        {
            object? D(string key) => detail.TryGetValue(key, out var value) ? value : null;
            html.Append("<tr>");
            foreach (var value in new[] { H("Shipper"), H("Mawb"), H("Hawb"), H("InvoiceNo"), H("Flt"), D("ItemNo"), D("Description"), D("Qty") })
                html.Append("<td style=\"border:1px solid #333;padding:5px\">").Append(E(value)).Append("</td>");
            html.Append("</tr>");
        }
        return html.Append("</tbody></table>").ToString();
    }
}
