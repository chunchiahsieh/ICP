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

        return value switch
        {
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            byte[] bytes => Convert.ToHexString(bytes),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }
}
