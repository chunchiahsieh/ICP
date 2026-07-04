using System.Reflection;
using ICP.Models.ShipInfo;

namespace ICP.Helpers;

public static class ShipInfoFieldBinding
{
    private static readonly Dictionary<string, PropertyInfo> HeaderDtoProperties =
        BuildDtoPropertyMap(typeof(ShipInfoHeaderRowDto));

    private static readonly Dictionary<string, PropertyInfo> DetailDtoProperties =
        BuildDtoPropertyMap(typeof(ShipInfoDetailRowDto));

    public static IReadOnlyDictionary<string, PropertyInfo> GetHeaderDtoProperties() => HeaderDtoProperties;

    public static IReadOnlyDictionary<string, PropertyInfo> GetDetailDtoProperties() => DetailDtoProperties;

    public static bool IsKnownHeaderField(string fieldName) =>
        !string.IsNullOrWhiteSpace(fieldName) && HeaderDtoProperties.ContainsKey(fieldName);

    public static bool IsKnownDetailField(string fieldName) =>
        !string.IsNullOrWhiteSpace(fieldName) && DetailDtoProperties.ContainsKey(fieldName);

    public static bool IsComputed(string fieldName, bool isHeader) =>
        TryGetDtoProperty(fieldName, isHeader, out var property)
        && property!.GetCustomAttribute<ShipInfoComputedAttribute>() is not null;

    public static string? ResolveEntityPropertyName(string fieldName, bool isHeader)
    {
        if (!TryGetDtoProperty(fieldName, isHeader, out var property) || property is null)
        {
            return null;
        }

        if (property.GetCustomAttribute<ShipInfoComputedAttribute>() is not null)
        {
            return null;
        }

        var mapped = property.GetCustomAttribute<ShipInfoMapsToEntityAttribute>();
        return mapped?.EntityPropertyName ?? property.Name;
    }

    public static string ResolveEntityPropertyName(ShipInfoFieldMetadata field, bool isHeader) =>
        field.EntityPropertyName
        ?? ResolveEntityPropertyName(field.FieldName, isHeader)
        ?? field.FieldName;

    private static bool TryGetDtoProperty(string fieldName, bool isHeader, out PropertyInfo? property)
    {
        var map = isHeader ? HeaderDtoProperties : DetailDtoProperties;
        return map.TryGetValue(fieldName, out property);
    }

    private static Dictionary<string, PropertyInfo> BuildDtoPropertyMap(Type dtoType) =>
        dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.CanRead && x.GetIndexParameters().Length == 0)
            .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
}
