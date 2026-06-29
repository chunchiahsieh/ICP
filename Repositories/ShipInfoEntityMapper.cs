using System.Globalization;
using System.Reflection;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.ShipInfo;

namespace ICP.Repositories;

public static class ShipInfoEntityMapper
{
    public static Dictionary<string, object?> MapEntity(object entity)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (property.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute>() is not null)
            {
                continue;
            }

            var value = property.GetValue(entity);
            result[property.Name] = value;
        }

        switch (entity)
        {
            case IcpHeader header:
                EnrichHeader(result, header);
                break;
            case IcpDetail detail:
                EnrichDetail(result, detail);
                break;
        }

        return result;
    }

    private static void EnrichHeader(Dictionary<string, object?> result, IcpHeader header)
    {
        var headerKey = ShipInfoKeyHelper.BuildHeaderKey(header);
        var headerRowKey = ShipInfoKeyHelper.BuildHeaderRowKey(header);
        var status = ShipInfoStatusResolver.Resolve(header);
        result["HeaderKey"] = headerKey;
        result["HeaderRowKey"] = headerRowKey;
        result["RowId"] = header.Id;
        result["Id"] = headerRowKey;
        result["Status"] = status;
        result["SaDateFrom"] = header.SaDate;
        result["EtaFrom"] = header.Eta;
        result["ShipNo"] = header.TetPo;
        result["Customer"] = header.EndUser ?? header.SoldToParty;
        result["DepositNo"] = header.Deposit;
        result["ArurNo"] = header.RtNo;
        result["Flight"] = header.Flt;
        result["Remark"] = header.Notes ?? header.SapRemarks;
    }

    private static void EnrichDetail(Dictionary<string, object?> result, IcpDetail detail)
    {
        var detailKey = ShipInfoKeyHelper.BuildDetailKey(detail);
        var headerKey = ShipInfoKeyHelper.BuildHeaderKey(detail.InvoiceNo);
        result["DetailKey"] = detailKey;
        result["RowId"] = detail.Id;
        result["Id"] = detailKey;
        result["HeaderKey"] = headerKey;
        result["LineNo"] = detail.InvoiceSeq;
        result["MaterialCode"] = detail.ItemNo;
        result["Quantity"] = detail.Qty;
        result["Weight"] = detail.GrossWeight ?? (object?)detail.NetWeightOfTheItem;
        result["InvoiceQty"] = detail.Qty;
        result["Carton"] = detail.CartonNo;
    }

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
            ApplyFieldValue(entity, field, values);
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
                var fromKey = field.FieldName + "From";
                if (values.TryGetValue(fromKey, out var fromValue))
                {
                    header.GetType().GetProperty(field.FieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                        ?.SetValue(header, NormalizeDateValue(fromValue));
                }

                continue;
            }

            if (!string.Equals(field.FieldName, "Status", StringComparison.OrdinalIgnoreCase))
            {
                ApplyFieldValue(header, field, values);
                continue;
            }

            if (!values.TryGetValue("Status", out var rawStatus))
            {
                continue;
            }

            var normalized = ShipInfoStatusResolver.Normalize(rawStatus);
            header.Status = normalized;
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
        IReadOnlyDictionary<string, string?> values)
    {
        if (!values.TryGetValue(field.FieldName, out var rawValue))
        {
            return;
        }

        var property = entity.GetType().GetProperty(field.FieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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
        var changes = new List<FieldChange>();
        foreach (var field in fields)
        {
            if (string.Equals(field.ControlType, ShipInfoControlTypes.DateRange, StringComparison.OrdinalIgnoreCase))
            {
                var dateProperty = entity.GetType().GetProperty(field.FieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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

            var property = entity.GetType().GetProperty(field.FieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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
