using ICP.Models.Icp;

namespace ICP.Helpers;

public static class ShipInfoKeyHelper
{
    private const char Separator = '\u001f';

    /// <summary>Header 與 Detail 關聯鍵（僅 INVOICE_NO）。</summary>
    public static string BuildHeaderKey(string invoiceNo) =>
        (invoiceNo ?? string.Empty).Trim();

    public static string BuildHeaderKey(IcpHeader header) =>
        BuildHeaderKey(header.InvoiceNo);

    /// <summary>Header 資料列唯一識別（INVOICE_NO + TET_PO）。</summary>
    public static string BuildHeaderRowKey(string invoiceNo, string tetPo) =>
        $"{(invoiceNo ?? string.Empty).Trim()}{Separator}{(tetPo ?? string.Empty).Trim()}";

    public static string BuildHeaderRowKey(IcpHeader header) =>
        BuildHeaderRowKey(header.InvoiceNo, header.TetPo);

    public static string ParseInvoiceNo(string? headerKey)
    {
        if (string.IsNullOrWhiteSpace(headerKey))
        {
            throw new ArgumentException("Header key is required.", nameof(headerKey));
        }

        var trimmed = headerKey.Trim();
        var separatorIndex = trimmed.IndexOf(Separator);
        return separatorIndex >= 0 ? trimmed[..separatorIndex] : trimmed;
    }

    public static (string InvoiceNo, string TetPo) ParseHeaderRowKey(string? headerRowKey)
    {
        if (string.IsNullOrWhiteSpace(headerRowKey))
        {
            throw new ArgumentException("Header row key is required.", nameof(headerRowKey));
        }

        var parts = headerRowKey.Split(Separator, 2, StringSplitOptions.None);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new ArgumentException("Header row key is invalid.", nameof(headerRowKey));
        }

        return (parts[0], parts[1]);
    }

    public static string BuildDetailKey(
        string invoiceNo,
        string tetPo,
        string tetPoLine,
        double invoiceSeq,
        string itemNo) =>
        string.Join(
            Separator,
            invoiceNo.Trim(),
            tetPo.Trim(),
            tetPoLine.Trim(),
            invoiceSeq.ToString(System.Globalization.CultureInfo.InvariantCulture),
            itemNo.Trim());

    public static string BuildDetailKey(IcpDetail detail) =>
        BuildDetailKey(
            detail.InvoiceNo,
            detail.TetPo,
            detail.TetPoLine ?? string.Empty,
            detail.InvoiceSeq ?? 0,
            detail.ItemNo ?? string.Empty);

    public static (string InvoiceNo, string TetPo, string TetPoLine, double InvoiceSeq, string ItemNo) ParseDetailKey(string? detailKey)
    {
        if (string.IsNullOrWhiteSpace(detailKey))
        {
            throw new ArgumentException("Detail key is required.", nameof(detailKey));
        }

        var parts = detailKey.Split(Separator);
        if (parts.Length != 5)
        {
            throw new ArgumentException("Detail key is invalid.", nameof(detailKey));
        }

        if (!double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var invoiceSeq))
        {
            throw new ArgumentException("Detail key is invalid.", nameof(detailKey));
        }

        return (parts[0], parts[1], parts[2], invoiceSeq, parts[4]);
    }
}
