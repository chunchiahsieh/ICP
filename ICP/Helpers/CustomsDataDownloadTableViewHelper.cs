using System.Globalization;
using System.Reflection;
using ICP.Models.CustomsDataDownload;
using ICP.Models.Icp;

namespace ICP.Helpers;

public static class CustomsDataDownloadTableViewHelper
{
    private static readonly PropertyInfo[] Properties =
        typeof(StgRawShippingAdvice).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    public static string ResolveHeaderLabel(CustomsDataDownloadTableFieldMetadata field, Func<string, string> localize) =>
        localize(field.HeaderLabelKey);

    public static bool IsNumericCell(StgRawShippingAdvice item, string fieldName)
    {
        var property = Properties.FirstOrDefault(p => string.Equals(p.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (property is null) return false;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (type == typeof(int) || type == typeof(double) || type == typeof(decimal) || type == typeof(float) || type == typeof(long))
            return true;
        return type == typeof(string) && decimal.TryParse(property.GetValue(item) as string,
            NumberStyles.Number, CultureInfo.InvariantCulture, out _);
    }

    public static string FormatCellValue(StgRawShippingAdvice item, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return string.Empty;
        }

        var property = Properties.FirstOrDefault(p =>
            string.Equals(p.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (property is null)
        {
            return string.Empty;
        }

        var value = property.GetValue(item);
        if (value is null)
        {
            return string.Empty;
        }

        var decimals = fieldName.ToUpperInvariant() switch
        {
            "QTY" => 3,
            "PRICE" or "AMOUNT" => 2,
            "GROSSWEIGHT" or "NETWEIGHTOFTHEITEM" => 4,
            _ => (int?)null
        };
        if (decimals.HasValue && value is IFormattable number)
            return number.ToString($"F{decimals.Value}", CultureInfo.InvariantCulture) ?? string.Empty;

        return value switch
        {
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            byte[] bytes => Convert.ToHexString(bytes),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }
}
