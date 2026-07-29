namespace ICP.Helpers;

/// <summary>
/// Maps DISA Excel headers (CREATE_DATE, INVOICE_NO, …) to logical property keys.
/// </summary>
public static class AddDiSaExcelColumnMap
{
    public const string InvoiceNo = "InvoiceNo";
    public const string TetPo = "TetPo";
    public const string Mawb = "Mawb";
    public const string Hawb = "Hawb";
    public const string Flt = "Flt";
    public const string Eta = "Eta";

    private static readonly HashSet<string> HeaderProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreateDate", "SaDate", "InvoiceNo", "Forwarder", "Broker", "Etd", "Eta", "InvoiceDate",
        "Mawb", "Hawb", "Flt", "Freight", "DestinationPort", "DestinationCountry", "Warehouse",
        "InvoiceType", "Incoterms", "OrderType", "DeliveryDate", "DeliveryTo", "Bu", "TetPo",
        "OrderPriority", "MdpFlag", "TotalCartons", "NcdrNo", "NcdrRequestor", "EndUserCode",
        "EndUser", "RtNo", "Receiver", "Owner", "MachineNo", "MachineType", "ShipReason",
        "Forklift", "MovingLabor", "CarMethod", "ArriveTime", "WasteDisposal", "DriverDetails",
        "OrderReason", "ArrivalNoticeFlag", "ArrivalNotice", "ReasonForDeliveryDelay",
        "DelayNotificationDate", "DeliveryNo", "SoldToPartyCode", "SoldToParty", "ShipToPartyCode",
        "ShipToParty", "ShipToPartyAddress", "EmgFlight", "WbsElement", "Deposit", "SapRemarks",
        "Notes", "Cancellation", "ReasonForCancellation", "AttachedFile"
    };

    private static readonly HashSet<string> DetailProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "TetPoLine", "InvoiceSeq", "ItemNo", "Description", "Qty", "Uom", "Coo", "Price", "Amount",
        "Currency", "Rate", "PackingType", "CartonNo", "Length", "Width", "Hight", "GrossWeight",
        "NetWeightOfTheItem", "DeliveryLineNo", "Eccn", "ElFlag", "SdsFlag", "Hazmat"
    };

    private static readonly HashSet<string> DateProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreateDate", "SaDate", "Etd", "Eta", "InvoiceDate", "DeliveryDate", "DelayNotificationDate"
    };

    public static bool IsHeaderProperty(string propertyKey) => HeaderProperties.Contains(propertyKey);

    public static bool IsDetailProperty(string propertyKey) => DetailProperties.Contains(propertyKey);

    public static bool IsDateProperty(string propertyKey) => DateProperties.Contains(propertyKey);

    public static bool TryResolve(string excelHeader, out string propertyKey)
    {
        propertyKey = string.Empty;
        if (string.IsNullOrWhiteSpace(excelHeader))
        {
            return false;
        }

        var normalized = excelHeader.Trim();
        if (normalized.Contains('_', StringComparison.Ordinal))
        {
            var parts = normalized.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            propertyKey = string.Concat(parts.Select(ToPascalPart));
        }
        else
        {
            propertyKey = ToPascalPart(normalized);
        }

        return HeaderProperties.Contains(propertyKey) || DetailProperties.Contains(propertyKey);
    }

    private static string ToPascalPart(string part)
    {
        if (string.IsNullOrEmpty(part))
        {
            return string.Empty;
        }

        if (part.Length == 1)
        {
            return part.ToUpperInvariant();
        }

        return char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant();
    }
}
