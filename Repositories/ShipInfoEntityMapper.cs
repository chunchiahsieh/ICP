using System.Globalization;
using System.Reflection;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.ShipInfo;

namespace ICP.Repositories;

public static class ShipInfoEntityMapper
{
    public static Dictionary<string, object?> MapEntity(object entity) =>
        entity switch
        {
            IcpHeader header => ShipInfoRowDtoMapper.MapHeader(header),
            IcpDetail detail => ShipInfoRowDtoMapper.MapDetail(detail),
            _ => throw new ArgumentException($"Unsupported entity type: {entity.GetType().Name}", nameof(entity))
        };

    public static void ApplyEditableValues(
        object entity,
        IReadOnlyDictionary<string, string?> values,
        IReadOnlyList<ShipInfoFieldMetadata> fields)
    {
        if (entity is IcpHeader header)
        {
            ApplyHeaderEditableValues(header, values, fields);
            return;
        }

        foreach (var field in fields.Where(x => x.Editable))
        {
            ApplyFieldValue(entity, field, values, isHeader: false);
        }
    }

    private static void ApplyHeaderEditableValues(
        IcpHeader header,
        IReadOnlyDictionary<string, string?> values,
        IReadOnlyList<ShipInfoFieldMetadata> fields)
    {
        foreach (var field in fields.Where(x => x.Editable))
        {
            if (string.Equals(field.ControlType, ShipInfoControlTypes.DateRange, StringComparison.OrdinalIgnoreCase))
            {
                var entityPropertyName = ShipInfoFieldBinding.ResolveEntityPropertyName(field, isHeader: true);
                var fromKey = field.FieldName + "From";
                if (values.TryGetValue(fromKey, out var fromValue) && !string.IsNullOrWhiteSpace(entityPropertyName))
                {
                    header.GetType().GetProperty(entityPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                        ?.SetValue(header, NormalizeDateValue(fromValue));
                }

                continue;
            }

            if (!string.Equals(field.FieldName, "Status", StringComparison.OrdinalIgnoreCase))
            {
                ApplyFieldValue(header, field, values, isHeader: true);
                continue;
            }

            if (!values.TryGetValue("Status", out var rawStatus))
            {
                continue;
            }

            var normalized = ShipInfoStatusResolver.Normalize(rawStatus);
            if (string.Equals(normalized, ShipInfoStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            {
                header.Cancellation = "Y";
            }
            else
            {
                header.Cancellation = null;
            }
        }
    }

    private static void ApplyFieldValue(
        object entity,
        ShipInfoFieldMetadata field,
        IReadOnlyDictionary<string, string?> values,
        bool isHeader)
    {
        if (!values.TryGetValue(field.FieldName, out var rawValue))
        {
            return;
        }

        var entityPropertyName = ShipInfoFieldBinding.ResolveEntityPropertyName(field, isHeader);
        if (string.IsNullOrWhiteSpace(entityPropertyName))
        {
            return;
        }

        var property = entity.GetType().GetProperty(entityPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        property.SetValue(entity, ConvertToPropertyValue(property.PropertyType, field, rawValue));
    }

    private static string? NormalizeDateValue(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var trimmed = rawValue.Trim();
        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return trimmed.Length > 10 ? trimmed[..10] : trimmed;
    }

    public static IReadOnlyList<FieldChange> DetectChanges(
        object entity,
        IReadOnlyDictionary<string, string?> values,
        IReadOnlyList<ShipInfoFieldMetadata> fields)
    {
        var isHeader = entity is IcpHeader;
        var changes = new List<FieldChange>();
        foreach (var field in fields)
        {
            if (string.Equals(field.ControlType, ShipInfoControlTypes.DateRange, StringComparison.OrdinalIgnoreCase))
            {
                var dateEntityProperty = ShipInfoFieldBinding.ResolveEntityPropertyName(field, isHeader);
                if (string.IsNullOrWhiteSpace(dateEntityProperty))
                {
                    continue;
                }

                var dateProperty = entity.GetType().GetProperty(dateEntityProperty, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (dateProperty is null || !dateProperty.CanRead)
                {
                    continue;
                }

                values.TryGetValue(field.FieldName + "From", out var dateIncomingRaw);
                var dateOldValue = FormatPropertyValue(dateProperty.GetValue(entity));
                var dateNewValue = FormatPropertyValue(NormalizeDateValue(dateIncomingRaw));
                if (!string.Equals(dateOldValue, dateNewValue, StringComparison.Ordinal))
                {
                    changes.Add(new FieldChange(field.FieldName, dateOldValue, dateNewValue));
                }

                continue;
            }

            var entityPropertyName = ShipInfoFieldBinding.ResolveEntityPropertyName(field, isHeader);
            if (string.IsNullOrWhiteSpace(entityPropertyName))
            {
                continue;
            }

            var property = entity.GetType().GetProperty(entityPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null || !property.CanRead)
            {
                continue;
            }

            values.TryGetValue(field.FieldName, out var incomingRaw);
            var oldValue = FormatPropertyValue(property.GetValue(entity));
            var newValue = FormatPropertyValue(ConvertToPropertyValue(property.PropertyType, field, incomingRaw));
            if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                changes.Add(new FieldChange(field.FieldName, oldValue, newValue));
            }
        }

        return changes;
    }

    public static string FormatPropertyValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static object? ConvertToPropertyValue(Type propertyType, ShipInfoFieldMetadata field, string? rawValue)
    {
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (Nullable.GetUnderlyingType(propertyType) is not null || !propertyType.IsValueType)
            {
                return null;
            }

            return Activator.CreateInstance(propertyType);
        }

        var trimmed = rawValue.Trim();
        if (string.Equals(field.FieldName, "DepositCaseStatus", StringComparison.OrdinalIgnoreCase)
            || string.Equals(field.FieldName, "ArurCaseStatus", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = ShipInfoCaseStatusResolver.Normalize(trimmed);
        }

        if (underlyingType == typeof(string))
        {
            return trimmed;
        }

        if (underlyingType == typeof(int))
        {
            return int.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(long))
        {
            return long.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(decimal))
        {
            return decimal.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(double))
        {
            return double.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(float))
        {
            return float.Parse(trimmed, CultureInfo.InvariantCulture);
        }

        if (underlyingType == typeof(DateTime))
        {
            if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var invariantDate))
            {
                return invariantDate;
            }

            return DateTime.Parse(trimmed, CultureInfo.CurrentCulture);
        }

        if (underlyingType == typeof(bool))
        {
            return bool.TryParse(trimmed, out var boolValue) && boolValue;
        }

        return Convert.ChangeType(trimmed, underlyingType, CultureInfo.InvariantCulture);
    }
}

public sealed record FieldChange(string FieldName, string OldValue, string NewValue);
