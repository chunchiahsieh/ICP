using System.Globalization;
using ICP.Models.Icp;

namespace ICP.Helpers;

public static class AddDiSaImportRules
{
    public static readonly string[] RequiredProperties =
    [
        "SaDate", "InvoiceNo", "Forwarder", "Broker", "Shipper", "Etd", "Eta", "InvoiceDate",
        "Mawb", "Hawb", "Flt", "Freight", "DestinationPort", "DestinationCountry", "Warehouse",
        "InvoiceType", "Incoterms", "OrderType", "DeliveryTo", "Bu", "InvoiceSeq", "ItemNo",
        "Description", "Qty", "Uom", "Coo", "Price", "Amount", "Currency", "Rate", "TotalCartons", "CartonNo", "GrossWeight"
    ];
    public static readonly HashSet<string> VariableInvoiceFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "InvoiceSeq", "ItemNo", "Description", "Qty", "Uom", "Coo", "Price", "Amount",
        "DeliveryLineNo", "Eccn", "ElFlag", "SdsFlag", "Hazmat", "WbsElement", "NetWeightOfTheItem"
    };
    public static readonly HashSet<string> SystemManagedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreateDate", "RtNo", "ArrivalNoticeFlag", "ArrivalNotice", "ReasonForDeliveryDelay",
        "DelayNotificationDate", "Deposit", "Notes", "Cancellation", "ReasonForCancellation", "AttachedFile"
    };
    public static readonly IReadOnlyDictionary<string, string> SettingCategories = new Dictionary<string, string>
    {
        ["Broker"] = "Broker", ["Shipper"] = "Shipper", ["Freight"] = "AirSea", ["Warehouse"] = "WhCode",
        ["InvoiceType"] = "InvoiceType", ["OrderType"] = "OrderType", ["DeliveryTo"] = "DeliveryToList", ["Bu"] = "BuCode"
    };

    public const int InvoiceNoMaxLength = 30;
    public const int TetPoMaxLength = 35;

    public static void ThrowIfErrors(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Take(30)));
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
        if (DateOnly.TryParseExact(trimmed, new[] { "yyyy-MM-dd", "yyyy/MM/dd" }, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dateOnly))
            return dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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

    public static void ValidateValues(Dictionary<string, string?> values, int rowNumber, List<string> errors)
    {
        foreach (var field in RequiredProperties)
            RequireNonEmpty(values.GetValueOrDefault(field), rowNumber, ExcelName(field), errors);
        foreach (var (field, text) in values.ToArray())
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            var type = AddDiSaExcelColumnMap.IsDetailProperty(field) ? typeof(IcpDetail) : typeof(IcpHeader);
            var property = type.GetProperty(field, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (property is null) continue;
            var max = property.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.MaxLengthAttribute), true)
                .Cast<System.ComponentModel.DataAnnotations.MaxLengthAttribute>().FirstOrDefault()?.Length;
            if (max.HasValue && text.Length > max.Value)
                errors.Add($"第 {rowNumber} 列 {ExcelName(field)} 超過 {max} 字元");
            var numericType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (numericType == typeof(decimal))
            {
                if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    if (field.Equals("GrossWeight", StringComparison.OrdinalIgnoreCase)
                        && (number < 0 || number > 9999999999.999m || decimal.Round(number, 3) != number))
                        errors.Add($"第 {rowNumber} 列 GROSS_WEIGHT 須為 0 至 9999999999.999，最多 3 位小數");
                    values[field] = number.ToString(CultureInfo.InvariantCulture);
                }
                else errors.Add($"第 {rowNumber} 列 {ExcelName(field)} 須為有效數字");
            }
            else if (numericType == typeof(double))
            {
                if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
                    values[field] = number.ToString("R", CultureInfo.InvariantCulture);
                else errors.Add($"第 {rowNumber} 列 {ExcelName(field)} 須為有效數字");
            }
            else if (numericType == typeof(int) && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                errors.Add($"第 {rowNumber} 列 {ExcelName(field)} 須為整數");
        }
        if (values.GetValueOrDefault("MdpFlag") is { Length: > 0 } flag && flag != "Y")
            errors.Add($"第 {rowNumber} 列 MDP_FLAG 僅允許 Y 或空白");
    }

    public static string ExcelName(string property) =>
        System.Text.RegularExpressions.Regex.Replace(property, "([a-z0-9])([A-Z])", "$1_$2").ToUpperInvariant();

    public static List<string> GetInvoiceConsistencyErrors(IReadOnlyList<AddDiSaImportRow> rows)
    {
        var errors = new List<string>();
        foreach (var group in rows.GroupBy(r => r.InvoiceNo, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            foreach (var row in group.Skip(1))
            {
                foreach (var field in first.Values.Keys.Union(row.Values.Keys, StringComparer.OrdinalIgnoreCase))
                {
                    if (VariableInvoiceFields.Contains(field) || SystemManagedFields.Contains(field)) continue;
                    var expected = first.Values.GetValueOrDefault(field) ?? "";
                    var actual = row.Values.GetValueOrDefault(field) ?? "";
                    if (!string.Equals(expected, actual, StringComparison.Ordinal))
                        errors.Add($"Invoice {group.Key} 第 {first.RowNumber}/{row.RowNumber} 列 {ExcelName(field)} 不一致（{expected}／{actual}）");
                }
            }
        }
        return errors;
    }
    public static void ValidateInvoiceConsistency(IReadOnlyList<AddDiSaImportRow> rows, List<string> errors) =>
        errors.AddRange(GetInvoiceConsistencyErrors(rows));

    public static HashSet<string> CollectInvoiceConsistencyIssues(IReadOnlyList<AddDiSaImportRow> rows) =>
        rows.GroupBy(r => r.InvoiceNo, StringComparer.OrdinalIgnoreCase)
            .Where(group => GetInvoiceConsistencyErrors(group.ToList()).Count > 0)
            .Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static void ValidateDuplicateSequences(IReadOnlyList<AddDiSaImportRow> rows, List<string> errors)
    {
        foreach (var group in rows.GroupBy(row => (row.InvoiceNo.ToUpperInvariant(), row.Detail.InvoiceSeq)).Where(group => group.Count() > 1))
            errors.Add($"INVOICE_NO＋INVOICE_SEQ 重複：{group.First().InvoiceNo}／{group.First().Detail.InvoiceSeq}，Excel 列 {string.Join(",", group.Select(row => row.RowNumber))}");
    }

}

public sealed class AddDiSaImportRow
{
    public Dictionary<string, string?> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);

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

    public int ControlledGoodsScheduledCount { get; init; }

    public int ControlledGoodsSkippedEtaCount { get; init; }
}
