using System.Text;

namespace ICP.Helpers;

public static class MassUpdateExcelColumnMap
{
    public const string InvoiceNo = "InvoiceNo";

    public static readonly IReadOnlyList<string> UpdateProperties =
    [
        "ArrivalNotice", "SaDate", "Forwarder", "Broker", "Eta", "Mawb", "Hawb", "Flt",
        "DeliveryDate", "MdpFlag", "ReasonForDeliveryDelay", "DelayNotificationDate"
    ];

    public static readonly HashSet<string> DateProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "SaDate", "Eta", "DeliveryDate", "DelayNotificationDate"
    };

    private static readonly Dictionary<string, string> Columns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["invoiceno"] = InvoiceNo,
        ["ncdrno"] = "NcdrNo",
        ["owner"] = "Owner",
        ["enduser"] = "EndUser",
        ["arrivalnotice"] = "ArrivalNotice",
        ["sadate"] = "SaDate",
        ["forwarder"] = "Forwarder",
        ["broker"] = "Broker",
        ["eta"] = "Eta",
        ["mawb"] = "Mawb",
        ["hawb"] = "Hawb",
        ["flt"] = "Flt",
        ["deliverydate"] = "DeliveryDate",
        ["mdpflag"] = "MdpFlag",
        ["reasonfordeliverydelay"] = "ReasonForDeliveryDelay",
        ["delaynotificationdate"] = "DelayNotificationDate"
    };

    public static bool TryResolve(string? excelHeader, out string propertyKey) =>
        Columns.TryGetValue(Normalize(excelHeader), out propertyKey!);

    private static string Normalize(string? value)
    {
        var builder = new StringBuilder();
        foreach (var character in value ?? string.Empty)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }
}
