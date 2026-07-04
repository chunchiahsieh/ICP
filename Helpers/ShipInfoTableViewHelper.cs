using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using ICP.Models.ShipInfo;

namespace ICP.Helpers;

public static class ShipInfoTableViewHelper
{
    private static readonly JsonSerializerOptions RowJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static IReadOnlyList<ShipInfoFieldMetadata> GetVisibleFields(IReadOnlyList<ShipInfoFieldMetadata> fields) =>
        fields.Where(x => x.Visible).OrderBy(x => x.DisplayOrder).ThenBy(x => x.FieldName).ToList();

    public static string ResolveLabel(ShipInfoFieldMetadata field, string? culture)
    {
        var normalizedCulture = (culture ?? "zh-TW").ToLowerInvariant();
        if (normalizedCulture.StartsWith("zh", StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(field.DisplayNameZh) ? field.DisplayName : field.DisplayNameZh;
        }

        return string.IsNullOrWhiteSpace(field.DisplayName) ? field.DisplayNameZh : field.DisplayName;
    }

    public static string FormatCellValue(object? value, ShipInfoFieldMetadata field)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (field.ControlType is ShipInfoControlTypes.Date or ShipInfoControlTypes.DateRange)
        {
            if (value is DateTime dateTime)
            {
                return dateTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            }

            if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            }
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static object? GetItemValue(IReadOnlyDictionary<string, object?> item, string fieldName)
    {
        if (item.TryGetValue(fieldName, out var value))
        {
            return value;
        }

        foreach (var pair in item)
        {
            if (pair.Key.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    public static string SerializeRowData(IReadOnlyDictionary<string, object?> item) =>
        JsonSerializer.Serialize(item, RowJsonOptions);
}
