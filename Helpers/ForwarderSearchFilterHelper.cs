using ICP.Models;
using ICP.Models.Forwarder;
using ICP.Services;

namespace ICP.Helpers;

public static class ForwarderSearchFilterHelper
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ParseColumnFilters(
        IFormCollection form,
        IReadOnlyList<ForwarderTableFieldMetadata> fields)
    {
        var filters = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields.Where(field => field.Searchable))
        {
            if (string.Equals(field.FieldName, "DuplicateStatus", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsCheckboxFilter(field))
            {
                continue;
            }

            var paramName = ForwarderTableMetadataProvider.ResolveFilterQueryParam(field.FieldName);
            var values = form[paramName]
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (values.Count > 0)
            {
                filters[field.FieldName] = values;
            }
        }

        return filters;
    }

    public static IReadOnlyList<string> GetDistinctValues(
        IReadOnlyList<ForwarderDataUploadRowViewModel> rows,
        string fieldName,
        string? search = null)
    {
        if (string.IsNullOrWhiteSpace(fieldName)
            || string.Equals(fieldName, "DuplicateStatus", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fieldName, "RowNo", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        IEnumerable<string> values = rows
            .Select(row => ForwarderTableViewHelper.FormatCellValue(row, fieldName))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(search))
        {
            values = values.Where(value => value.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return values
            .Take(SearchFilterHelper.FilterOptionsLimit)
            .ToList();
    }

    public static IReadOnlyList<ForwarderDataUploadRowViewModel> ApplyColumnFilters(
        IReadOnlyList<ForwarderDataUploadRowViewModel> rows,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters)
    {
        if (filters.Count == 0)
        {
            return rows;
        }

        var normalizedFilters = filters
            .Where(pair => pair.Value.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair => new HashSet<string>(pair.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        if (normalizedFilters.Count == 0)
        {
            return rows;
        }

        return rows
            .Where(row => normalizedFilters.All(pair =>
            {
                var cellValue = ForwarderTableViewHelper.FormatCellValue(row, pair.Key);
                return pair.Value.Contains(cellValue);
            }))
            .ToList();
    }

    public static bool IsCheckboxFilter(ForwarderTableFieldMetadata field) =>
        string.IsNullOrWhiteSpace(field.FilterType)
        || string.Equals(field.FilterType, "Checkbox", StringComparison.OrdinalIgnoreCase);
}
