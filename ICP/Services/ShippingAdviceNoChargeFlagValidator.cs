using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace ICP.Services;

public static class ShippingAdviceNoChargeFlagValidator
{
    static ShippingAdviceNoChargeFlagValidator()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static void Validate(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = ExcelReaderFactory.CreateReader(stream);
        if (!reader.Read())
            throw new InvalidOperationException("Shipping advice Excel 沒有標題列。");

        var invoiceColumn = -1;
        var flagColumn = -1;
        for (var column = 0; column < reader.FieldCount; column++)
        {
            var title = CellText(reader.GetValue(column));
            if (title.Equals("Invoice No.", StringComparison.OrdinalIgnoreCase)) invoiceColumn = column;
            if (title.Equals("No Charge Flag", StringComparison.OrdinalIgnoreCase)) flagColumn = column;
        }
        if (invoiceColumn < 0 || flagColumn < 0)
            throw new InvalidOperationException("Shipping advice Excel 缺少 Invoice No. 或 No Charge Flag 欄位。");

        var firstByInvoice = new Dictionary<string, (bool IsNoCharge, int RowNumber)>(StringComparer.OrdinalIgnoreCase);
        var rowNumber = 1;
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            var invoice = CellText(reader.GetValue(invoiceColumn));
            if (invoice.Length == 0) continue;

            var isNoCharge = CellText(reader.GetValue(flagColumn)).Equals("X", StringComparison.OrdinalIgnoreCase);
            if (!firstByInvoice.TryGetValue(invoice, out var first))
            {
                firstByInvoice.Add(invoice, (isNoCharge, rowNumber));
                continue;
            }
            if (first.IsNoCharge != isNoCharge)
                throw new InvalidOperationException(
                    $"Invoice {invoice} 的 No Charge Flag 不一致：第 {first.RowNumber} 列為{FlagText(first.IsNoCharge)}，第 {rowNumber} 列為{FlagText(isNoCharge)}。請修正後再上傳。");
        }
    }

    private static string CellText(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

    private static string FlagText(bool isNoCharge) => isNoCharge ? " X" : "非 X";
}
