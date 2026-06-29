using System.Globalization;
using System.Text.RegularExpressions;

namespace ICP.Models.ShipInfo;

public static class ShipInfoMetadataHelper
{
    public static string ResolveDisplayName(ShipInfoFieldMetadata field, string? culture = null)
    {
        var normalizedCulture = (culture ?? CultureInfo.CurrentUICulture.Name).ToLowerInvariant();
        if (normalizedCulture.StartsWith("zh", StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(field.DisplayNameZh))
            {
                return field.DisplayNameZh;
            }
        }

        if (!string.IsNullOrWhiteSpace(field.DisplayName))
        {
            return field.DisplayName;
        }

        return field.FieldName;
    }

    public static IReadOnlyList<ShipInfoFieldMetadata> OrderFields(IEnumerable<ShipInfoFieldMetadata> fields) =>
        fields
            .Where(x => x.Visible)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<ShipInfoFieldMetadata> GetSearchFields(IEnumerable<ShipInfoFieldMetadata> headerFields) =>
        headerFields
            .Where(x => x.Searchable)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string ResolveSearchControlType(ShipInfoFieldMetadata field) =>
        string.IsNullOrWhiteSpace(field.SearchControlType)
            ? field.ControlType
            : field.SearchControlType;

    public static IReadOnlyList<string> ValidateFieldValues(
        IReadOnlyList<ShipInfoFieldMetadata> fields,
        IReadOnlyDictionary<string, string?> values,
        bool validateEditableOnly = true,
        string? culture = null)
    {
        var errors = new List<string>();

        foreach (var field in fields)
        {
            if (validateEditableOnly && !field.Editable)
            {
                continue;
            }

            if (string.Equals(field.ControlType, ShipInfoControlTypes.DateRange, StringComparison.OrdinalIgnoreCase))
            {
                values.TryGetValue(field.FieldName + "From", out var fromValue);
                values.TryGetValue(field.FieldName + "To", out var toValue);
                errors.AddRange(ValidateSingleField(field, fromValue, culture));
                if (!string.IsNullOrWhiteSpace(fromValue) && !string.IsNullOrWhiteSpace(toValue)
                    && DateTime.TryParse(fromValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var fromDate)
                    && DateTime.TryParse(toValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var toDate)
                    && fromDate > toDate)
                {
                    errors.Add($"{ResolveDisplayName(field, culture)} end date must be on or after start date.");
                }

                continue;
            }

            values.TryGetValue(field.FieldName, out var rawValue);
            errors.AddRange(ValidateSingleField(field, rawValue, culture));
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateSingleField(
        ShipInfoFieldMetadata field,
        string? rawValue,
        string? culture = null)
    {
        var errors = new List<string>();
        var value = rawValue?.Trim();
        var label = ResolveDisplayName(field, culture);

        if (field.Required && string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{label} is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return errors;
        }

        if (field.MinLength.HasValue && value.Length < field.MinLength.Value)
        {
            errors.Add($"{label} minimum length is {field.MinLength.Value}.");
        }

        if (field.MaxLength.HasValue && value.Length > field.MaxLength.Value)
        {
            errors.Add($"{label} maximum length is {field.MaxLength.Value}.");
        }

        if (!string.IsNullOrWhiteSpace(field.Regex) && !Regex.IsMatch(value, field.Regex))
        {
            errors.Add($"{label} format is invalid.");
        }

        if (IsNumericControl(field.ControlType))
        {
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
            {
                errors.Add($"{label} must be a number.");
            }
            else
            {
                if (field.MinValue.HasValue && decimalValue < field.MinValue.Value)
                {
                    errors.Add($"{label} minimum value is {field.MinValue.Value}.");
                }

                if (field.MaxValue.HasValue && decimalValue > field.MaxValue.Value)
                {
                    errors.Add($"{label} maximum value is {field.MaxValue.Value}.");
                }
            }
        }

        if (IsDateControl(field.ControlType) && !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out _)
            && !DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out _))
        {
            errors.Add($"{label} date format is invalid.");
        }

        return errors;
    }

    public static IReadOnlyList<string> DetectNonEditableChanges(
        IReadOnlyList<ShipInfoFieldMetadata> fields,
        IReadOnlyDictionary<string, string?> submittedValues,
        IReadOnlyDictionary<string, string?> currentValues,
        string? culture = null)
    {
        var errors = new List<string>();
        foreach (var field in fields.Where(x => !x.Editable))
        {
            submittedValues.TryGetValue(field.FieldName, out var submitted);
            currentValues.TryGetValue(field.FieldName, out var current);
            if (!string.Equals(Normalize(submitted), Normalize(current), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{ResolveDisplayName(field, culture)} is read only.");
            }
        }

        return errors;
    }

    private static bool IsNumericControl(string controlType) =>
        controlType.Equals(ShipInfoControlTypes.Number, StringComparison.OrdinalIgnoreCase)
        || controlType.Equals(ShipInfoControlTypes.Decimal, StringComparison.OrdinalIgnoreCase)
        || controlType.Equals(ShipInfoControlTypes.Currency, StringComparison.OrdinalIgnoreCase);

    private static bool IsDateControl(string controlType) =>
        controlType.Equals(ShipInfoControlTypes.Date, StringComparison.OrdinalIgnoreCase)
        || controlType.Equals(ShipInfoControlTypes.DateTime, StringComparison.OrdinalIgnoreCase)
        || controlType.Equals(ShipInfoControlTypes.DateRange, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value) => (value ?? string.Empty).Trim();
}
