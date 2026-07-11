using System.Globalization;
using ICP.Models.Icp;

namespace ICP.Helpers;

public static class AddDiSaImportRules
{
    public static readonly string[] RequiredProperties =
    [
        AddDiSaExcelColumnMap.InvoiceNo,
        AddDiSaExcelColumnMap.TetPo
    ];

    public static readonly string[] InvoiceConsistencyProperties =
    [
        AddDiSaExcelColumnMap.Mawb,
        AddDiSaExcelColumnMap.Hawb,
        AddDiSaExcelColumnMap.Flt,
        AddDiSaExcelColumnMap.Eta
    ];

    public const int InvoiceNoMaxLength = 30;
    public const int TetPoMaxLength = 35;

    public static void ThrowIfErrors(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(string.Join("；", errors.Take(30)));
    }

    /// <summary>
    /// Treat Excel literal "NULL" (any case) as empty so it maps to SQL NULL.
    /// </summary>
    public static string? NormalizeCellText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }

    public static string? NormalizeDateString(string? text, int rowNumber, string fieldName, List<string> errors)
    {
        text = NormalizeCellText(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly)
            || DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateOnly)
            || DateOnly.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out dateOnly))
        {
            return dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var oa)
            && oa > 20000 && oa < 60000)
        {
            try
            {
                var fromOa = DateTime.FromOADate(oa);
                return DateOnly.FromDateTime(fromOa).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch
            {
                // fall through
            }
        }

        errors.Add($"第 {rowNumber} 列 {fieldName} 日期格式不正確（需 yyyy-MM-dd）");
        return null;
    }

    public static string RequireNonEmpty(string? value, int rowNumber, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"第 {rowNumber} 列缺少必填欄位 {fieldName}");
            return string.Empty;
        }

        return value.Trim();
    }

    public static string? TrimToMax(string? value, int maxLength)
    {
        value = NormalizeCellText(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public static void ValidateInvoiceConsistency(IReadOnlyList<AddDiSaImportRow> rows, List<string> errors)
    {
        foreach (var invoiceNo in CollectInvoiceConsistencyIssues(rows))
        {
            errors.Add($"Invoice {invoiceNo} 的 MAWB/HAWB/FLT/ETA 不一致");
        }
    }

    /// <summary>
    /// Returns InvoiceNo values whose MAWB/HAWB/FLT/ETA are not consistent across rows (soft, no throw).
    /// </summary>
    public static HashSet<string> CollectInvoiceConsistencyIssues(IReadOnlyList<AddDiSaImportRow> rows)
    {
        var inconsistent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in rows.GroupBy(r => r.InvoiceNo, StringComparer.OrdinalIgnoreCase))
        {
            var list = group.ToList();
            if (list.Count <= 1)
            {
                continue;
            }

            var first = list[0];
            foreach (var prop in InvoiceConsistencyProperties)
            {
                var expected = GetConsistencyValue(first, prop);
                foreach (var row in list.Skip(1))
                {
                    var actual = GetConsistencyValue(row, prop);
                    if (!string.Equals(expected, actual, StringComparison.Ordinal))
                    {
                        inconsistent.Add(group.Key);
                        break;
                    }
                }

                if (inconsistent.Contains(group.Key))
                {
                    break;
                }
            }
        }

        return inconsistent;
    }

    private static string GetConsistencyValue(AddDiSaImportRow row, string property) =>
        property switch
        {
            AddDiSaExcelColumnMap.Mawb => NormalizeCompare(row.Mawb),
            AddDiSaExcelColumnMap.Hawb => NormalizeCompare(row.Hawb),
            AddDiSaExcelColumnMap.Flt => NormalizeCompare(row.Flt),
            AddDiSaExcelColumnMap.Eta => NormalizeCompare(row.Eta),
            _ => string.Empty
        };

    private static string NormalizeCompare(string? value) => (value ?? string.Empty).Trim();
}

public sealed class AddDiSaImportRow
{
    public int RowNumber { get; init; }

    public string InvoiceNo { get; set; } = string.Empty;

    public string TetPo { get; set; } = string.Empty;

    public string? Mawb { get; set; }

    public string? Hawb { get; set; }

    public string? Flt { get; set; }

    public string? Eta { get; set; }

    public IcpHeader Header { get; init; } = new();

    public IcpDetail Detail { get; init; } = new();
}

public sealed class AddDiSaImportRowViewModel
{
    public required AddDiSaImportRow Row { get; init; }

    public bool IsDbDuplicate { get; init; }

    public bool IsInvoiceInconsistent { get; init; }

    public bool CanUpload => !IsDbDuplicate && !IsInvoiceInconsistent;
}

public sealed class AddDiSaImportResult
{
    public int HeaderCount { get; init; }

    public int DetailCount { get; init; }
}
