using System.Globalization;
using System.Reflection;
using ICP.Models.Icp;
using ICP.Models.Tariff;

namespace ICP.Helpers;

public static class TariffTableViewHelper
{
    private static readonly IReadOnlyDictionary<string, PropertyInfo> Properties =
        typeof(TariffData).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

    public static string ResolveHeaderLabel(TariffTableFieldMetadata field, Func<string, string> localize) =>
        localize(field.HeaderLabelKey);

    public static string FormatCellValue(TariffData item, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName)
            || TariffMetadataHelper.IsVirtualField(fieldName))
        {
            return string.Empty;
        }

        if (!Properties.TryGetValue(fieldName, out var property))
        {
            return string.Empty;
        }

        if (string.Equals(fieldName, nameof(TariffData.CreateUser), StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(item.CreateUserDisplayName) ? item.CreateUser ?? string.Empty : item.CreateUserDisplayName;
        }

        return property.GetValue(item) switch
        {
            null => string.Empty,
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            IFormattable value => value.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            var value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }
}
