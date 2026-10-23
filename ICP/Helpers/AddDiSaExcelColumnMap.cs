using System.Globalization;
using System.Reflection;

namespace ICP.Helpers;

/// <summary>
/// Maps DISA Excel headers (CREATE_DATE, INVOICE_NO, …) to logical property keys.
/// </summary>
public static class AddDiSaExcelColumnMap
{
    /// <summary>Upload Data columns in the order used by the download template and preview.</summary>
    public static IReadOnlyList<string> PreviewColumns { get; } = Array.AsReadOnly(
        ("SA_DATE INVOICE_NO FORWARDER BROKER SHIPPER ETD ETA INVOICE_DATE MAWB HAWB FLT FREIGHT " +
         "DESTINATION_PORT DESTINATION_COUNTRY WAREHOUSE INVOICE_TYPE INCOTERMS ORDER_TYPE DELIVERY_DATE DELIVERY_TO BU " +
         "TET_PO TET_PO_LINE ORDER_PRIORITY INVOICE_SEQ ITEM_NO DESCRIPTION QTY UOM COO PRICE AMOUNT CURRENCY RATE " +
         "MDP_FLAG TOTAL_CARTONS CARTON_NO LENGTH WIDTH HIGHT GROSS_WEIGHT NET_WEIGHT_OF_THE_ITEM PACKING_TYPE " +
         "NCDR_NO NCDR_REQUESTOR END_USER_CODE END_USER RECEIVER OWNER MACHINE_NO MACHINE_TYPE SHIP_REASON FORKLIFT " +
         "MOVING_LABOR CAR_METHOD ARRIVE_TIME WASTE_DISPOSAL DRIVER_DETAILS ORDER_REASON DELIVERY_NO DELIVERY_LINE_NO " +
         "SOLD_TO_PARTY_CODE SOLD_TO_PARTY SHIP_TO_PARTY_CODE SHIP_TO_PARTY SHIP_TO_PARTY_ADDRESS ECCN EL_FLAG SDS_FLAG " +
         "EMG_FLIGHT HAZMAT WBS_ELEMENT SAP_REMARKS").Split(' '));

    public const string InvoiceNo = "InvoiceNo";
    public const string TetPo = "TetPo";
    public const string Shipper = "Shipper";
    public const string Mawb = "Mawb";
    public const string Hawb = "Hawb";
    public const string Flt = "Flt";
    public const string Eta = "Eta";

    private static readonly HashSet<string> HeaderProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreateDate", "SaDate", "InvoiceNo", "Forwarder", "Broker", "Shipper", "Etd", "Eta", "InvoiceDate",
        "Mawb", "Hawb", "Flt", "Freight", "DestinationPort", "DestinationCountry", "Warehouse",
        "InvoiceType", "Incoterms", "OrderType", "DeliveryDate", "DeliveryTo", "Bu", "TetPo",
        "OrderPriority", "MdpFlag", "TotalCartons", "NcdrNo", "NcdrRequestor", "EndUserCode",
        "EndUser", "RtNo", "Receiver", "Owner", "MachineNo", "MachineType", "ShipReason",
        "Forklift", "MovingLabor", "CarMethod", "ArriveTime", "WasteDisposal", "DriverDetails",
        "OrderReason", "ArrivalNoticeFlag", "ArrivalNotice", "ReasonForDeliveryDelay",
        "DelayNotificationDate", "DeliveryNo", "SoldToPartyCode", "SoldToParty", "ShipToPartyCode",
        "ShipToParty", "ShipToPartyAddress", "EmgFlight", "Deposit", "SapRemarks",
        "Notes", "Cancellation", "ReasonForCancellation", "AttachedFile"
    };

    private static readonly HashSet<string> DetailProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "TetPoLine", "InvoiceSeq", "ItemNo", "Description", "Qty", "Uom", "Coo", "Price", "Amount",
        "Currency", "Rate", "PackingType", "CartonNo", "Length", "Width", "Hight", "GrossWeight",
        "NetWeightOfTheItem", "DeliveryLineNo", "Eccn", "ElFlag", "SdsFlag", "Hazmat", "WbsElement"
    };

    private static readonly HashSet<string> DateProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreateDate", "SaDate", "Etd", "Eta", "InvoiceDate", "DeliveryDate", "DelayNotificationDate"
    };

    public static bool IsHeaderProperty(string propertyKey) => HeaderProperties.Contains(propertyKey);

    public static bool IsDetailProperty(string propertyKey) => DetailProperties.Contains(propertyKey);

    public static bool IsDateProperty(string propertyKey) => DateProperties.Contains(propertyKey);

    public static string GetPreviewValue(AddDiSaImportRow row, string column)
    {
        if (!TryResolve(column, out var propertyKey)) return string.Empty;

        // Render the actual parsed entities rather than depending on the auxiliary Values dictionary.
        // Rows created by older import flows can have an empty Values dictionary.
        object source = IsHeaderProperty(propertyKey) ? row.Header : row.Detail;
        var property = source.GetType().GetProperty(propertyKey,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return Convert.ToString(property?.GetValue(source), CultureInfo.InvariantCulture) ?? string.Empty;
    }

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
