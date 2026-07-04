namespace ICP.Models.ShipInfo;

public static class ShipInfoTableFilterColumns
{
    private static readonly HashSet<string> HeaderColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Status",
        "CreateDate",
        "SaDate",
        "InvoiceNo",
        "Broker",
        "Eta"
    };

    private static readonly HashSet<string> DetailColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "InvoiceSeq",
        "DepositCaseStatus",
        "ArurCaseStatus",
        "TetPoLine",
        "ItemNo",
        "Description",
        "Qty",
        "Uom",
        "Coo",
        "Price",
        "Amount",
        "Currency",
        "CartonNo",
        "GrossWeight"
    };

    public static bool IsHeaderAllowed(string column) =>
        !string.IsNullOrWhiteSpace(column) && HeaderColumns.Contains(column);

    public static bool IsDetailAllowed(string column) =>
        !string.IsNullOrWhiteSpace(column) && DetailColumns.Contains(column);
}
